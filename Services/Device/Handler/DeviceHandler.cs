using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NetworkSoundBox.Authorization.Device;
using NetworkSoundBox.Entities;
using NetworkSoundBox.Hubs;
using NetworkSoundBox.Services.DTO;
using NetworkSoundBox.Services.Message;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.Device.Handler
{
    public class DeviceHandler
    {
        private readonly IDeviceAuthorization _deviceAuthorization;
        private readonly INotificationContext _notificationContext;

        public const int MAX_RETRY_TIMES = 3;
        public const int DEFAULT_TIMEOUT_LONG = 15 * 1000;

        public string SN { get; private set; }
        public DeviceType Type { get;private set; }
        public string UserOpenId { get; private set; }
        public Socket Socket { get; }
        public IPAddress IPAddress { get; private set; }
        public int Port { get; private set; }
        public CancellationTokenSource CTS { get; private set; }
        public BlockingCollection<File> FileQueue { get => _fileQueue; }
        public BlockingCollection<Inbound> InboxQueue { get => _inboxQueue; }
        public readonly List<Token> TokenList = new();

        private readonly byte[] _receiveBuffer;
        private readonly List<byte> _data;
        private readonly BlockingCollection<Outbound> _outboxQueue;
        private readonly BlockingCollection<Inbound> _inboxQueue;
        private readonly BlockingCollection<File> _fileQueue;
        private readonly Task _handleDeviceTask;
        private readonly Task _handleInboxTask;
        private readonly Task _handleOutboxTask;
        private readonly Task _handleFilesTask;
        private int _fileCount = 0;
        private RetryManager heartbeat;

        public DeviceHandler(Socket socket,
                             INotificationContext notificationContext,
                             IDeviceAuthorization deviceAuthorization)
        {
            _notificationContext = notificationContext;
            _deviceAuthorization = deviceAuthorization;

            Socket = socket;
            Socket.ReceiveTimeout = 1;
            Socket.SendTimeout = 5000;

            SN = "";
            IPAddress = ((IPEndPoint)socket.RemoteEndPoint).Address;
            Port = ((IPEndPoint)socket.RemoteEndPoint).Port;
            _receiveBuffer = new byte[300];
            _data = new List<byte>();

            CTS = new CancellationTokenSource();
            _outboxQueue = new BlockingCollection<Outbound>(20);
            _fileQueue = new BlockingCollection<File>(20);
            _inboxQueue = new BlockingCollection<Inbound>(20);

            _handleDeviceTask = HandleDevice();
            _handleInboxTask = HandleInbox();
            _handleOutboxTask = HandleOutbox();
            _handleFilesTask = HandleFiles();
        }

        #region 设备后台任务
        /// <summary>
        /// 设备Socket任务
        /// </summary>
        /// <returns></returns>
        private async Task HandleDevice()
        {
            await Task.Yield();
            Console.WriteLine("Device @{0}:{1} has connected!", IPAddress, Port);
            Timer loginTimeout = new(new TimerCallback(LoginTimeoutCallback), this, 10000, Timeout.Infinite);
            heartbeat = new(1, DEFAULT_TIMEOUT_LONG, new(o => CTS.Cancel()));
            Timer heartbeatTimeout = new(new TimerCallback(HeartbeatTimeoutCallback), this, 75000, 65000);
            int receiveCount = 0;
            while (true)
            {
                if (CTS.IsCancellationRequested)
                {
                    Console.WriteLine("Device {2}@{0}:{1} has disconnected!", IPAddress, Port, SN == "" ? "" : $"[{SN}]");
                    Socket.Shutdown(SocketShutdown.Both);
                    Socket.Close();
                    Socket.Dispose();
                    var logoutTime = DateTime.Now;
                    if (DeviceContext._devicePool.ContainsKey(SN))
                    {
                        DeviceContext._devicePool.Remove(SN);
                        _handleFilesTask.Wait();
                        _handleOutboxTask.Wait();
                        _handleInboxTask.Wait();
                        using MySqlDbContext dbContext = new(new DbContextOptionsBuilder<MySqlDbContext>().Options);
                        var deviceEntity = dbContext.Devices.FirstOrDefault(d => d.Sn == SN);
                        if (deviceEntity != null)
                        {
                            deviceEntity.LastOnline = logoutTime;
                            dbContext.Update(deviceEntity);
                            dbContext.SaveChanges();
                        }
                        await _notificationContext.SendDeviceOffline(UserOpenId, SN);
                    }
                    return;
                }
                try
                {
                    receiveCount = Socket.Receive(_receiveBuffer);
                    if (receiveCount == 0)
                    {
                        CTS.Cancel();
                    }
                    else
                    {
                        for (int cnt = 0; cnt < receiveCount; cnt++)
                        {
                            _data.Add(_receiveBuffer[cnt]);
                        }
                    }
                }
                catch (SocketException ex)
                {
                    switch (ex.SocketErrorCode)
                    {
                        case SocketError.TimedOut:
                            if (_data.Count != 0)
                            {
                                Inbound.ParseMessage(_data, this);
                                _data.Clear();
                            }
                            break;
                        case SocketError.ConnectionReset:
                            Console.WriteLine("Cancel for connection reseted");
                            CTS.Cancel();
                            break;
                        default:
                            Console.WriteLine(ex.Message + "Error Code: {0}", ex.ErrorCode);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// 设备收件箱任务
        /// </summary>
        /// <returns></returns>
        private async Task HandleInbox()
        {
            await Task.Yield();
            Inbound message = null;
            while (true)
            {
                message = null;
                try
                {
                    message = _inboxQueue.Take(CTS.Token);
                    Token token = GetToken(message.Command);
                    heartbeat?.Reset();
                    Console.WriteLine("[{0}] Recv [{1}] Bytes CMD[0x{2:X2}]", SN, message.MessageLen, (int)message.Command);
                    message.Data.ForEach(x =>
                    {
                        Console.Write("0x{0:X2}", x);
                        Console.Write(' ');
                    });
                    Console.WriteLine();
                    switch (message.Command)
                    {
                        case Command.LOGIN:
                            if (SN != "") break;

                            if (!_deviceAuthorization.Authorize(message.Data))
                            {
                                Console.WriteLine("Authorization failed");
                                CTS.Cancel();
                                break;
                            }

                            SN = Encoding.ASCII.GetString(message.Data.GetRange(0, 8).ToArray());
                            byte tempDeviceType = message.Data[^1];
                            if (!Enum.IsDefined(typeof(DeviceType), (int)tempDeviceType))
                            {
                                Console.WriteLine("Invalid Device-Type");
                            }
                            Type = (DeviceType)tempDeviceType;
                            using (MySqlDbContext dbContext = new(new DbContextOptionsBuilder<MySqlDbContext>().Options))
                            {
                                var deviceEntity = dbContext.Devices.Include(d => d.User).FirstOrDefault(d => d.Sn == SN);
                                if (deviceEntity == null)
                                {
                                    CTS.Cancel();
                                    Console.WriteLine($"Invalid SN \"{SN}\", socket @{IPAddress}:{Port} will be closed!");
                                    break;
                                }
                                UserOpenId = deviceEntity.User.Openid;
                                if (deviceEntity.DeviceType == "TEST")
                                {
                                    deviceEntity.DeviceType = Enum.GetName(Type);
                                    dbContext.SaveChanges();
                                }
                                await _notificationContext.SendDeviceOnline(UserOpenId, SN);
                            }
                            DeviceHandler device = DeviceContext._devicePool.FirstOrDefault(pair => pair.Key == SN).Value;
                            if (device != null)
                            {
                                device.CTS.Cancel();
                                DeviceContext._devicePool.Remove(SN);
                                Console.WriteLine("Device with SN \"{0}\" has existed, renew device", SN);
                            }
                            DeviceContext._devicePool.Add(SN, this);
                            Console.WriteLine($"Device with SN \"{SN}\" has loged in, socket @{IPAddress}:{Port}. Now we have got {DeviceContext._devicePool.Count} devices.");
                            _outboxQueue.TryAdd(new Outbound(Command.LOGIN, null, _deviceAuthorization.GetAuthorization(SN)));
                            break;
                        case Command.HEARTBEAT:
                            Console.WriteLine("收到心跳信号");
                            _outboxQueue.Add(new Outbound(Command.HEARTBEAT));
                            break;
                        case Command.FILE_TRANS_ERR_WIFI:
                            if (token != null)
                            {
                                token.Data = message.Data.ToArray();
                                token.Status = MessageStatus.Failed;
                            }
                            break;
                        case Command.FILE_TRANS_REQ_WIFI:
                        case Command.FILE_TRANS_RPT_WIFI:
                        case Command.FILE_TRANS_REQ_CELL:
                        case Command.FILE_TRANS_RPT_CELL:
                        case Command.PLAY:
                        case Command.PAUSE:
                        case Command.NEXT:
                        case Command.PREVIOUS:
                        case Command.VOLUMN:
                        case Command.READ_FILES_LIST:
                        case Command.DELETE_FILE:
                        case Command.PLAY_INDEX:
                        case Command.REBOOT:
                        case Command.LOOP_WHILE:
                        case Command.QUERY_TIMING_MODE:
                        case Command.QUERY_TIMING_SET:
                        case Command.SET_TIMING_ALARM:
                        case Command.SET_TIMING_AFTER:
                        case Command.TIMING_REPORT:
                        case Command.FACTORY_RESET:
                            if (token != null)
                            {
                                token.Data = message.Data.ToArray();
                                token.Status = MessageStatus.Replied;
                            }
                            break;
                        default:
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Exit HandleInbox() Task");
                    return;
                }
            }
        }

        /// <summary>
        /// 设备发件箱任务
        /// </summary>
        /// <returns></returns>
        private async Task HandleOutbox()
        {
            await Task.Yield();
            Outbound message = null;
            RetryManager retry = new();
            while (true)
            {
                message = null;
                try
                {
                    message = _outboxQueue.Take(CTS.Token);
                    retry.Reset();
                    while (retry.Set())
                    {
                        if (CTS.IsCancellationRequested)
                            throw new OperationCanceledException();

                        message.Token.Status = MessageStatus.Sending;
                        try
                        {
                            switch (message.Command)
                            {
                                case Command.LOGIN:
                                case Command.HEARTBEAT:
                                case Command.REBOOT:
                                case Command.FACTORY_RESET:
                                case Command.LOOP_WHILE:
                                case Command.QUERY_TIMING_MODE:
                                case Command.QUERY_TIMING_SET:
                                case Command.SET_TIMING_ALARM:
                                case Command.SET_TIMING_AFTER:
                                case Command.TIMING_REPORT:
                                case Command.PLAY:
                                case Command.PAUSE:
                                case Command.NEXT:
                                case Command.PREVIOUS:
                                case Command.VOLUMN:
                                case Command.FAST_FORWARD:
                                case Command.FAST_BACKWARD:
                                case Command.PLAY_INDEX:
                                case Command.READ_FILES_LIST:
                                case Command.DELETE_FILE:
                                case Command.FILE_TRANS_REQ_WIFI:
                                case Command.FILE_TRANS_PROC_WIFI:
                                case Command.FILE_TRANS_RPT_WIFI:
                                case Command.FILE_TRANS_REQ_CELL:
                                case Command.FILE_TRANS_RPT_CELL:
                                    Socket.Send(message.Data.ToArray(), message.MessageLen, 0);
                                    message.Token.Status = MessageStatus.Sent;
                                    break;
                                default:
                                    throw new ArgumentException();
                            }
                            Console.WriteLine("[{0}] Trns [{1}] Bytes CMD[0x{2:X2}]", SN, message.MessageLen, (int)message.Command);
                            break;
                        }
                        catch (SocketException ex)
                        {
                            if (ex.SocketErrorCode != SocketError.TimedOut)
                                Console.WriteLine(ex.Message);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    if (message != null)
                        message.Token.Status = MessageStatus.Failed;
                    Console.WriteLine("Exit HandleOutbox() Task");
                    return;
                }
                catch (ObjectDisposedException)
                {
                    if (message != null)
                        message.Token.Status = MessageStatus.Failed;
                    return;
                }
                catch (ArgumentException)
                {
                    if (message != null)
                        message.Token.Status = MessageStatus.Failed;
                    continue;
                }
                catch (TimeoutException)
                {
                    continue;
                }
            }
        }

        /// <summary>
        /// 设备下发文件任务
        /// </summary>
        /// <returns></returns>
        private async Task HandleFiles()
        {
            await Task.Yield();
            Console.WriteLine("HandleFiles() Task is running...");
            File file = null;
            RetryManager retry = new();
            while (true)
            {
                file = null;
                try
                {
                    file = _fileQueue.Take(CTS.Token);

                    //发送 [请求传送文件] 命令
                    retry.Reset();
                    while (retry.Set())
                    {
                        Console.WriteLine("[{0}] Request to transmit file[{1}], retry:{2}", SN, _fileCount + 1, retry.Count);

                        Token token = new(
                            TokenList,
                            Command.FILE_TRANS_REQ_WIFI,//期待回复命令
                            Command.FILE_TRANS_ERR_WIFI,//错误回报命令
                            new byte[] { 0x00, 0x00 });//期待回复数据

                        _outboxQueue.Add(new(
                            Command.FILE_TRANS_REQ_WIFI,//待发送命令
                            token,
                            (byte)(_fileCount + 1),//文件序号
                            (byte)(file.PackageCount >> 8),//总包数
                            (byte)(file.PackageCount)));//总包数

                        //等待消息被发送
                        token.WaitSent();

                        //等待消息被回复
                        token.WaitReplied();

                        //检查回复内容
                        if (token.CheckReply()) break;
                    }

                    //循环下发文件
                    for (int pkgIdx = 0; pkgIdx < file.PackageCount;)
                    {
                        pkgIdx++;
                        var pkg = file.Packages.Dequeue();
                        await _notificationContext.SendDownloadProgress(UserOpenId, (float)(100.0 * pkgIdx / file.PackageCount));
                        retry.Reset();
                        while (retry.Set())
                        {

                            Console.WriteLine("[{0}] Transmitting {1}/{2} package of file[{3}], retry:{4}", SN, pkgIdx, file.PackageCount, _fileCount + 1, retry.Count);

                            Token token = new(
                                TokenList,
                                Command.FILE_TRANS_REQ_WIFI,//期待回复命令
                                Command.FILE_TRANS_ERR_WIFI,//错误回报命令
                                new byte[] { (byte)(pkgIdx >> 8), (byte)pkgIdx });//期待回复数据

                            _outboxQueue.Add(new(
                                Command.FILE_TRANS_PROC_WIFI,//待发送命令
                                pkgIdx,//包序号
                                token,
                                pkg));//文件数据

                            //等待消息被发送
                            token.WaitSent();

                            //等待消息被回复
                            token.WaitReplied();

                            //检查回复内容
                            if (token.CheckReply()) break;
                        }
                    }

                    retry.Reset();
                    //发送 [当前文件传输完成] 命令
                    while (retry.Set())
                    {
                        Console.WriteLine("[{0}] Transmition of file[{1}] is finished, sending EOF command, retry:{2}", SN, _fileCount + 1, retry.Count);

                        Token token = new(
                            TokenList,
                            Command.FILE_TRANS_RPT_WIFI,
                            Command.FILE_TRANS_ERR_WIFI,
                            new byte[] { 0x00, (byte)(_fileCount + 1) });

                        _outboxQueue.Add(new Outbound(
                            Command.FILE_TRANS_RPT_WIFI,
                            token,
                            0x00, (byte)(_fileCount + 1)));

                        //等待消息被发送
                        token.WaitSent();

                        //等待消息被回复
                        token.WaitReplied();

                        //检查回复内容
                        if (token.CheckReply())
                        {
                            _fileCount++;
                            file.Success();
                            break;
                        }
                    }

                    retry.Reset();
                    //发送 [文件全部更新完毕] 命令
                    if (_fileQueue.Count == 0)
                    {
                        while (retry.Set())
                        {
                            Console.WriteLine("[{0}] Transmition files are all set, notifying the device, retry: {1}", SN, retry.Count);

                            Token token = new(
                                TokenList,
                                Command.FILE_TRANS_RPT_WIFI,
                                Command.FILE_TRANS_RPT_WIFI,
                                new byte[] { 0x00, 0x00 });

                            _outboxQueue.Add(new Outbound(
                                Command.FILE_TRANS_RPT_WIFI,
                                token,
                                0x00, 0x00));

                            //等待消息被发送
                            token.WaitSent();

                            //等待消息被回复
                            token.WaitReplied();

                            //检查回复内容
                            if (token.CheckReply()) break;
                        }
                    }
                    retry.Reset();
                }
                catch (OperationCanceledException)
                {
                    if (file != null)
                    {
                        file.Fail();
                    }
                    while (_fileQueue.Count != 0)
                    {
                        _fileQueue.Take().Fail();
                    }
                    Console.WriteLine("Exit HandleFiles() Task");
                    return;
                }
                catch (TimeoutException)
                {
                    if (file != null)
                    {
                        file.Fail();
                    }
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    if (file != null)
                    {
                        file.Fail();
                    }
                    if (_fileQueue == null) { return; }
                    while (_fileQueue.Count != 0)
                    {
                        _fileQueue.Take().Fail();
                    }
                    return;
                }
                catch (Exception ex) { Console.WriteLine(ex.Message); }
            }
        }
        #endregion

        #region 播放控制
        /// <summary>
        /// 获取播放列表
        /// </summary>
        /// <returns>播放列表文件个数</returns>
        public int GetPlayList()
        {
            Token token = new(TokenList, Command.READ_FILES_LIST, null);
            Outbound outbound = new(Command.READ_FILES_LIST, token);
            try
            {
                _outboxQueue.Add(outbound, CTS.Token);
                token.WaitReplied();
                if (token.Data.Length != 2)
                { return -1; }
                return token.Data[0] | token.Data[1];
            }
            catch (Exception)
            { return -1; }
        }

        /// <summary>
        /// 删除指定序号的文件
        /// </summary>
        /// <param name="index">文件序号</param>
        /// <returns>True: 删除成功 False: 删除失败</returns>
        public bool DeleteAudio(int index)
        {
            byte[] data = new byte[] { (byte)(index >> 8), (byte)index };
            Token token = new(TokenList, Command.DELETE_FILE, null);
            Outbound outbound = new(Command.DELETE_FILE, token, data);
            try
            {
                _outboxQueue.Add(outbound, CTS.Token);
                token.WaitReplied();
                return token.CheckReply(data);
            }
            catch (Exception)
            { return false; }
        }

        /// <summary>
        /// 播放指定序号的音频
        /// </summary>
        /// <param name="index"></param>
        /// <returns>True: 成功 False: 失败</returns>
        public bool PlayIndex(int index)
        {
            byte[] data = new byte[] { (byte)(index << 8), (byte)index };
            Token token = new(TokenList, Command.PLAY_INDEX, null);
            Outbound outbound = new(Command.PLAY_INDEX, token, data);
            try
            {
                _outboxQueue.Add(outbound, CTS.Token);
                token.WaitReplied();
                return token.CheckReply(data);
            }
            catch (Exception)
            { return false; }
        }

        /// <summary>
        /// 发送播放或暂停命令
        /// </summary>
        /// <param name="action">1: 播放; 2: 暂停</param>
        /// <returns>true: 成功; false: 失败</returns>
        public bool SendPlayOrPause(int action)
        {
            Command command;
            if (action == 1)
                command = Command.PLAY;
            else
                command = Command.PAUSE;
            Token token = new(TokenList, command, null);
            Outbound outbound = new(command, token);
            try
            {
                _outboxQueue.Add(outbound, CTS.Token);
                token.WaitReplied();
                return token.Status == MessageStatus.Replied;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 发送下一首上一首命令
        /// </summary>
        /// <param name="action">1: 下一首; 2: 上一首</param>
        /// <returns></returns>
        public bool SendNextOrPrevious(int action)
        {
            Command command;
            if (action == 1)
                command = Command.NEXT;
            else
                command = Command.PREVIOUS;

            Token token = new(TokenList, command, null);
            Outbound outbound = new(command, token);
            try
            {
                _outboxQueue.Add(outbound, CTS.Token);
                token.WaitReplied();
                return token.Status == MessageStatus.Replied;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 发送音量命令
        /// </summary>
        /// <param name="volumn">音量(0~30)</param>
        /// <returns>true: 成功; false: 失败</returns>
        public bool SendVolumn(int volumn)
        {
            Token token = new(TokenList, Command.VOLUMN, null);
            Outbound outbound = new(Command.VOLUMN, token, 0x00, (byte)volumn);
            try
            {
                _outboxQueue.Add(outbound, CTS.Token);
                token.WaitReplied();
                return token.Status == MessageStatus.Replied;
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion

        #region 定时控制
        public bool SendCronTask(List<byte> data)
        {
            Token token = new(TokenList, Command.SET_TIMING_ALARM, null);
            Outbound outbound = new(Command.SET_TIMING_ALARM, token, data.ToArray());
            try
            {
                _outboxQueue.Add(outbound, CTS.Token);
                token.WaitReplied();
                return token.Status == MessageStatus.Replied;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool SendDelayTask(List<byte> data)
        {
            Token token = new(TokenList, Command.SET_TIMING_AFTER, null);
            Outbound outbound = new(Command.SET_TIMING_AFTER, token, data.ToArray());
            try
            {
                _outboxQueue.Add(outbound, CTS.Token);
                token.WaitReplied();
                return token.Status == MessageStatus.Replied;
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion

        #region 设备控制
        /// <summary>
        /// 发送重启命令
        /// </summary>
        /// <returns>true: 成功; false: 失败</returns>
        public bool SendReboot()
        {
            Token token = new(TokenList, Command.REBOOT, null);
            Outbound outbound = new(Command.REBOOT, token);
            try
            {
                _outboxQueue.Add(outbound, CTS.Token);
                token.WaitReplied();
                return token.Status == MessageStatus.Replied;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 发送恢复出厂命令
        /// </summary>
        /// <returns>true: 成功; false: 失败</returns>
        public bool SendRestore()
        {
            Token token = new(TokenList, Command.FACTORY_RESET, null);
            Outbound outbound = new(Command.FACTORY_RESET, token);
            try
            {
                _outboxQueue.Add(outbound, CTS.Token);
                token.WaitReplied();
                return token.Status == MessageStatus.Replied;
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion

        /// <summary>
        /// 4G设备传输文件通知
        /// </summary>
        /// <param name="fileToken"></param>
        /// <returns></returns>
        public bool ReqFileTrans(byte[] fileToken)
        {
            if (fileToken == null) return false;

            Token token = new(TokenList, Command.FILE_TRANS_REQ_CELL, null);
            Outbound outbound = new(Command.FILE_TRANS_REQ_CELL, token, fileToken);
            try
            {
                _outboxQueue.Add(outbound, CTS.Token);
                token.WaitReplied();
                return token.Status == MessageStatus.Replied;
            }
            catch(Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 登陆超时回调
        /// </summary>
        /// <param name="state"></param>
        private void LoginTimeoutCallback(object state)
        {
            if (CTS.IsCancellationRequested) return;
            if (SN == "")
            {
                CTS.Cancel();
                Console.WriteLine("Device @{0}:{1} login timeout!", ((IPEndPoint)Socket.RemoteEndPoint).Address, ((IPEndPoint)Socket.RemoteEndPoint).Port);
            }
        }

        /// <summary>
        /// 心跳超时回调
        /// </summary>
        /// <param name="state"></param>
        private void HeartbeatTimeoutCallback(object state)
        {
            if (heartbeat != null)
            {
                heartbeat.Set();
            }
        }

        /// <summary>
        /// 查找消息Token
        /// </summary>
        /// <param name="command">命令</param>
        /// <returns></returns>
        private Token GetToken(Command command)
        {
            if (command != Command.NONE)
                return TokenList.FirstOrDefault(token => token.ExpectCommand == command || token.ExceptionCommand == command);
            return null;
        }
    }
}
