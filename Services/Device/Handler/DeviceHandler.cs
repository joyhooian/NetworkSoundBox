using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetworkSoundBox.Entities;
using NetworkSoundBox.Middleware.Authorization.Device;
using NetworkSoundBox.Middleware.Hubs;
using NetworkSoundBox.Middleware.Logger;
using NetworkSoundBox.Services.Message;

namespace NetworkSoundBox.Services.Device.Handler
{
    public class DeviceHandler
    {
        private const int DefaultTimeoutLong = 15 * 1000;

        private readonly IDeviceAuthorization _deviceAuthorization;
        private readonly INotificationContext _notificationContext;
        private readonly IDeviceContext _deviceContext;
        private readonly IMessageContext _messageContext;
        private readonly ILogger<DeviceHandler> _logger;

        public string Sn { get; private set; }
        public Nsb.Type.DeviceType Type { get; private set; }
        public string UserOpenId { get; private set; }
        public IPAddress IpAddress { get; }
        public int Port { get; }
        public BlockingCollection<File> FileQueue { get; }
        public BlockingCollection<Inbound> InboxQueue { get; }
        public bool IsHbOverflow => _heartbeat.IsOverflow;
        
        private readonly Socket _socket;
        private readonly CancellationTokenSource _cts;
        private readonly Timer _loginTimer;
        private readonly Timer _heartbeatTimer;
        private readonly byte[] _receiveBuffer;
        private readonly BlockingCollection<Outbound> _outboxQueue;
        private int _fileCount;
        private readonly RetryManager _heartbeat;

        public DeviceHandler(
            ILogger<DeviceHandler> logger,
            Socket socket,
            IMessageContext messageContext,
            INotificationContext notificationContext,
            IDeviceAuthorization deviceAuthorization,
            IDeviceContext deviceContext)
        {
            _notificationContext = notificationContext;
            _deviceAuthorization = deviceAuthorization;
            _deviceContext = deviceContext;
            _messageContext = messageContext;
            _logger = logger;

            _socket = socket;
            _socket.SendTimeout = 5000;
            _cts = new CancellationTokenSource();
            _heartbeat = new RetryManager(3, DefaultTimeoutLong, _ => _cts.Cancel());
            _loginTimer = new Timer(LoginTimeoutCb, null, 10000, Timeout.Infinite);
            _heartbeatTimer = new Timer(HeartbeatTimeoutCb, null, 30000, 25000);

            Sn = "";
            IpAddress = ((IPEndPoint) socket.RemoteEndPoint)?.Address;
            Port = ((IPEndPoint) socket.RemoteEndPoint)!.Port;
            _receiveBuffer = new byte[300];

            _outboxQueue = new BlockingCollection<Outbound>(20);
            FileQueue = new BlockingCollection<File>(20);
            InboxQueue = new BlockingCollection<Inbound>(20);

            Task.Run(HandleInbox);
            Task.Run(HandleOutbox);
            Task.Run(HandleFiles);
            StartSocketCommunication();
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _loginTimer?.Dispose();
            _heartbeatTimer?.Dispose();
            _socket?.Close();
            _socket?.Dispose();
            FileQueue?.Dispose();
            InboxQueue?.Dispose();
            _outboxQueue?.Dispose();
        }

        #region 设备后台任务

        /// <summary>
        /// 设备收件箱任务
        /// </summary>
        /// <returns></returns>
        private void HandleInbox()
        {
            while (true)
            {
                try
                {
                    _heartbeat.Reset();
                    var message = InboxQueue.Take(_cts.Token);
                    _logger.LogInformation(LogEvent.MsgRecv, $"[{Sn}]Recv[{message?.MessageLen}]bytes,CMD[{message?.Command.ToString()}],Data:{Convert.ToHexString(message?.Data.ToArray())}");
                    //Console.WriteLine($"[{Sn}]收到长度{message.MessageLen}的数据 CMD[{message.Command}]");
                    //message.Data.ForEach(x => Console.Write("{0:X2} ", x));
                    //Console.WriteLine("");
                    var token = _messageContext.GetToken(message.Command);
                    switch (message.Command)
                    {
                        case Command.Login:
                            DeviceLogin(message);
                            break;
                        case Command.Heartbeat:
                            _outboxQueue.TryAdd(new Outbound(Command.Heartbeat));
                            break;
                        case Command.FileTransErrWifi:
                            token?.SetFailed();
                            break;
                        case Command.None:
                        case Command.Activation:
                        case Command.Reboot:
                        case Command.FactoryReset:
                        case Command.LoopWhile:
                        case Command.QueryTimingMode:
                        case Command.QueryTimingSet:
                        case Command.SetTimingAlarm:
                        case Command.SetTimingAfter:
                        case Command.TimingReport:
                        case Command.FileTransReqWifi:
                        case Command.FileTransProcWifi:
                        case Command.FileTransRptWifi:
                        case Command.FileTransReqCell:
                        case Command.FileTransRptCell:
                        case Command.Play:
                        case Command.Pause:
                        case Command.Next:
                        case Command.Previous:
                        case Command.Volume:
                        case Command.FastForward:
                        case Command.FastBackward:
                        case Command.PlayIndex:
                        case Command.ReadFilesList:
                        case Command.DeleteFile:
                            token?.SetData(message.Data);
                            token?.SetReplied();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception e)
                {
                    if (e is not OperationCanceledException)
                    {
                        _logger.LogError(LogEvent.MsgRecv, e, $"[{Sn}]");
                        //Console.WriteLine(e.Message);
                        continue;
                    }
                    //Console.WriteLine($"[{Sn}]退出收件任务");
                    return;
                }
            }
        }

        /// <summary>
        /// 设备发件箱任务
        /// </summary>
        /// <returns></returns>
        private void HandleOutbox()
        {
            RetryManager retry = new();
            while (true)
            {
                Outbound message = null;
                try
                {
                    message = _outboxQueue.Take(_cts.Token);
                    retry.Reset();
                    while (retry.Set())
                    {
                        if (_cts.IsCancellationRequested)
                            throw new OperationCanceledException();

                        message.Token?.SetSending();
                        switch (message.Command)
                        {
                            case Command.Login:
                            case Command.Heartbeat:
                            case Command.Reboot:
                            case Command.FactoryReset:
                            case Command.LoopWhile:
                            case Command.QueryTimingMode:
                            case Command.QueryTimingSet:
                            case Command.SetTimingAlarm:
                            case Command.SetTimingAfter:
                            case Command.TimingReport:
                            case Command.Play:
                            case Command.Pause:
                            case Command.Next:
                            case Command.Previous:
                            case Command.Volume:
                            case Command.FastForward:
                            case Command.FastBackward:
                            case Command.PlayIndex:
                            case Command.ReadFilesList:
                            case Command.DeleteFile:
                            case Command.FileTransReqWifi:
                            case Command.FileTransProcWifi:
                            case Command.FileTransRptWifi:
                            case Command.FileTransReqCell:
                            case Command.FileTransRptCell:
                                _socket.BeginSend(message.Data.ToArray(), 0, message.MessageLen, SocketFlags.None,
                                    SocketSendCb, message);
                                break;
                            default:
                                throw new ArgumentException();
                        }

                        break;
                    }
                }
                catch (Exception ex)
                {
                    if (ex is not OperationCanceledException)
                        _logger.LogError(LogEvent.MsgSend, ex, $"[{Sn}]");
                        //Console.WriteLine(ex.Message);
                    switch (ex)
                    {
                        case OperationCanceledException:
                        case ObjectDisposedException:
                            message?.Token?.SetFailed();
                            //Console.WriteLine($"[{Sn}]退出发信任务");
                            return;
                    }
                }
            }
        }

        /// <summary>
        /// 设备下发文件任务
        /// </summary>
        /// <returns></returns>
        private void HandleFiles()
        {
            RetryManager retry = new();
            while (true)
            {
                File file = null;
                try
                {
                    file = FileQueue.Take(_cts.Token);
                    //发送 [请求传送文件] 命令
                    retry.Reset();
                    while (retry.Set())
                    {
                        _logger.LogInformation(LogEvent.FileProc, $"[{Sn}]ReqFile[{_fileCount + 1}],Retry:{retry.Count}");
                        //Console.WriteLine($"[{Sn}]申请传送文件[{_fileCount + 1}] 第{retry.Count}次尝试");
                        MessageToken token = new(Command.FileTransReqWifi, new byte[] {0x00, 0x00});
                        _messageContext.SetToken(token);
                        _outboxQueue.Add(new Outbound(Command.FileTransReqWifi,
                                token,
                                (byte) (_fileCount + 1),
                                (byte) (file.PackageCount >> 8), //总包数
                                (byte) (file.PackageCount)), //总包数
                            _cts.Token);

                        if (MessageStatus.Replied == token.Wait() && token.IsValidate) break;
                    }

                    //循环下发文件
                    for (var pkgIdx = 0; pkgIdx < file.PackageCount;)
                    {
                        pkgIdx++;
                        var pkg = file.Packages.Dequeue();
                        _notificationContext.SendDownloadProgress(UserOpenId,
                            (float) (100.0 * pkgIdx / file.PackageCount));
                        retry.Reset();
                        while (retry.Set())
                        {
                            _logger.LogInformation(LogEvent.MsgSend, $"[{Sn}]SnedFile[{_fileCount + 1}],Proc:{pkgIdx:D3}/{file.PackageCount:D3},Retry:{retry.Count}");
                            //Console.WriteLine(
                            //    $"[{Sn}]传送{_fileCount + 1}号文件 第{pkgIdx:D3}包/共{file.PackageCount:D3}包 第{retry.Count}次尝试");
                            var token = new MessageToken(Command.FileTransReqWifi,
                                new[] {(byte) (pkgIdx >> 8), (byte) pkgIdx});
                            _messageContext.SetToken(token);
                            _outboxQueue.Add(new Outbound(Command.FileTransProcWifi, pkgIdx, token, pkg), _cts.Token);

                            if (MessageStatus.Replied == token.Wait() && token.IsValidate) break;
                        }
                    }

                    retry.Reset();
                    //发送 [当前文件传输完成] 命令
                    while (retry.Set())
                    {
                        _logger.LogInformation(LogEvent.FileProc, $"[{Sn}]SendFile{_fileCount + 1}Cplt,Send EOF,Retry:{retry.Count}");
                        //Console.WriteLine($"[{Sn}]传送{_fileCount + 1}号文件完成 发送文件结束命令 第{retry.Count}次尝试");
                        var token = new MessageToken(Command.FileTransRptWifi,
                            new byte[] {0x00, (byte) (_fileCount + 1)});
                        _messageContext.SetToken(token);

                        _outboxQueue.Add(new Outbound(Command.FileTransRptWifi, token, 0x00, (byte) (_fileCount + 1)),
                            _cts.Token);

                        if (MessageStatus.Replied != token.Wait() || !token.IsValidate) continue;
                        _fileCount++;
                        file.Success();
                        break;
                    }

                    retry.Reset();
                    //发送 [文件全部更新完毕] 命令
                    if (FileQueue.Count == 0)
                    {
                        while (retry.Set())
                        {
                            _logger.LogInformation(LogEvent.FileProc, $"[{Sn}]FileQueueClear,SendFlag,Retry:{retry.Count}");
                            //Console.WriteLine($"[{Sn}]所有文件下发完成 通知设备 第{retry.Count}次尝试");
                            var token = new MessageToken(Command.FileTransRptWifi, new byte[] {0x00, 0x00});
                            _messageContext.SetToken(token);

                            _outboxQueue.Add(new Outbound(Command.FileTransRptWifi, token, 0x00, 0x00), _cts.Token);

                            if (MessageStatus.Replied == token.Wait() && token.IsValidate) break;
                        }
                    }

                    retry.Reset();
                }
                catch (Exception ex)
                {
                    switch (ex)
                    {
                        case OperationCanceledException:
                            file?.Fail();
                            while (FileQueue?.Count != 0)
                            {
                                FileQueue.Take()?.Fail();
                            }
                            //Console.WriteLine($"[{Sn}]退出文件下载任务");
                            return;
                        case ObjectDisposedException:
                            file?.Fail();
                            while (FileQueue?.Count != 0)
                            {
                                FileQueue.Take().Fail();
                            }

                            return;
                        default:
                            _logger.LogError(LogEvent.FileProc, ex, $"[{Sn}]");
                            //Console.WriteLine(ex.Message);
                            break;
                    }
                }
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
            var token = new MessageToken(Command.ReadFilesList);
            _messageContext.SetToken(token);
            var outbound = new Outbound(Command.ReadFilesList, token);
            try
            {
                _outboxQueue.Add(outbound, _cts.Token);
                if (MessageStatus.Replied == token.Wait() && token.RepliedData.Length == 2)
                    return token.RepliedData[0] | token.RepliedData[1];
                return -1;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>
        /// 删除指定序号的文件
        /// </summary>
        /// <param name="index">文件序号</param>
        /// <returns>True: 删除成功 False: 删除失败</returns>
        public bool DeleteAudio(int index)
        {
            byte[] data = {(byte) (index >> 8), (byte) index};
            var token = new MessageToken(Command.DeleteFile);
            _messageContext.SetToken(token);
            Outbound outbound = new(Command.DeleteFile, token, data);
            try
            {
                _outboxQueue.Add(outbound, _cts.Token);
                return MessageStatus.Replied == token.Wait();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 播放指定序号的音频
        /// </summary>
        /// <param name="index"></param>
        /// <returns>True: 成功 False: 失败</returns>
        public bool PlayIndex(int index)
        {
            byte[] data = {(byte) (index << 8), (byte) index};
            var token = new MessageToken(Command.PlayIndex);
            _messageContext.SetToken(token);
            Outbound outbound = new(Command.PlayIndex, token, data);
            try
            {
                _outboxQueue.Add(outbound, _cts.Token);
                return MessageStatus.Replied == token.Wait();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 发送播放或暂停命令
        /// </summary>
        /// <param name="action">1: 播放; 2: 暂停</param>
        /// <returns>true: 成功; false: 失败</returns>
        public bool SendPlayOrPause(int action)
        {
            var command = action == 1 ? Command.Play : Command.Pause;
            var token = new MessageToken(command);
            _messageContext.SetToken(token);
            Outbound outbound = new(command, token);
            try
            {
                _outboxQueue.Add(outbound, _cts.Token);
                return MessageStatus.Replied == token.Wait();
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
            var command = action == 1 ? Command.Next : Command.Previous;
            var token = new MessageToken(command);
            _messageContext.SetToken(token);
            Outbound outbound = new(command, token);
            try
            {
                _outboxQueue.Add(outbound, _cts.Token);
                return MessageStatus.Replied == token.Wait();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 发送音量命令
        /// </summary>
        /// <param name="volume">音量(0~30)</param>
        /// <returns>true: 成功; false: 失败</returns>
        public bool SendVolume(int volume)
        {
            var token = new MessageToken(Command.Volume);
            _messageContext.SetToken(token);
            Outbound outbound = new(Command.Volume, token, 0x00, (byte) volume);
            try
            {
                _outboxQueue.Add(outbound, _cts.Token);
                return token.Wait() == MessageStatus.Replied;
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
            var token = new MessageToken(Command.SetTimingAlarm);
            _messageContext.SetToken(token);
            Outbound outbound = new(Command.SetTimingAlarm, token, data.ToArray());
            try
            {
                _outboxQueue.Add(outbound, _cts.Token);
                return token.Wait() == MessageStatus.Replied;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool SendDelayTask(List<byte> data)
        {
            var token = new MessageToken(Command.SetTimingAfter);
            _messageContext.SetToken(token);
            Outbound outbound = new(Command.SetTimingAfter, token, data.ToArray());
            try
            {
                _outboxQueue.Add(outbound, _cts.Token);
                return token.Wait() == MessageStatus.Replied;
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
            var token = new MessageToken(Command.Reboot);
            _messageContext.SetToken(token);
            Outbound outbound = new(Command.Reboot, token);
            try
            {
                _outboxQueue.Add(outbound, _cts.Token);
                return token.Wait() == MessageStatus.Replied;
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
            var token = new MessageToken(Command.FactoryReset);
            _messageContext.SetToken(token);
            Outbound outbound = new(Command.FactoryReset, token);
            try
            {
                _outboxQueue.Add(outbound, _cts.Token);
                return token.Wait() == MessageStatus.Replied;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

        /// <summary>
        /// 打开Socket通讯
        /// </summary>
        private void StartSocketCommunication()
        {
            _logger.LogInformation(LogEvent.DeviceConn, $"Device @{IpAddress}:{Port}has connected");
            //Console.WriteLine($"Device @{IpAddress}:{Port} has connected!");
            try
            {
                _socket.BeginReceive(_receiveBuffer, 0, _receiveBuffer.Length, SocketFlags.None, SocketRcvCb, this);
            }
            catch (Exception e)
            {
                _logger.LogError(LogEvent.DeviceConn, e, $"@{IpAddress}:{Port}");
                //Console.WriteLine(e);
            }
        }

        /// <summary>
        /// Socket接收回调
        /// </summary>
        /// <param name="ar"></param>
        /// <exception cref="Exception"></exception>
        private void SocketRcvCb(IAsyncResult ar)
        {
            try
            {
                var count = _socket.EndReceive(ar);
                if (_cts.IsCancellationRequested) throw new Exception("Canceled");
                if (count > 0)
                {
                    Inbound.ParseMessage(_receiveBuffer[..count].ToList(), this);
                    _socket.BeginReceive(_receiveBuffer, 0, _receiveBuffer.Length, SocketFlags.None, SocketRcvCb, this);
                }
                else throw new Exception("Receive 0 byte from device, time to close");
            }
            catch (Exception e)
            {
                //Console.WriteLine(e.Message);
                _logger.LogInformation(LogEvent.DeviceDisconn, $"Device {Sn} @{IpAddress}:{Port} disconnect");
                //Console.WriteLine($"Device {Sn} @{IpAddress}:{Port} disconnect");
                Close();
            }
        }

        /// <summary>
        /// Socket发送回调
        /// </summary>
        /// <param name="ar"></param>
        /// <exception cref="Exception"></exception>
        private void SocketSendCb(IAsyncResult ar)
        {
            try
            {
                var count = _socket.EndSend(ar);
                var msg = ar.AsyncState as Outbound;
                if (count <= 0)
                {
                    _logger.LogError(LogEvent.MsgSend, $"[{Sn}]SendFailed CMD[{msg?.Command.ToString()}]");
                }
                _logger.LogInformation(LogEvent.MsgSend, $"[{Sn}]Send[{msg?.MessageLen}]bytes CMD[{msg?.Command.ToString()}]");
                //Console.WriteLine($"[{Sn}]发送长度{msg?.MessageLen}的数据 CMD[{msg?.Command.ToString()}]");
                msg?.Token?.SetSent();
                msg?.Data?.ForEach(x => Console.Write($"{x:X2} "));
                Console.WriteLine();
            }
            catch (Exception e)
            {
                _logger.LogError(LogEvent.MsgSend, e, $"[{Sn}]");
                switch (e)
                {
                    case SocketException:
                    case ObjectDisposedException:
                        Close();
                        break;
                }
            }
        }

        /// <summary>
        /// 4G设备传输文件通知
        /// </summary>
        /// <param name="fileToken"></param>
        /// <returns></returns>
        public bool ReqFileTrans(byte[] fileToken)
        {
            if (fileToken == null) return false;

            var token = new MessageToken(Command.FileTransReqCell);
            _messageContext.SetToken(token);
            Outbound outbound = new(Command.FileTransReqCell, token, fileToken);
            try
            {
                _outboxQueue.Add(outbound, _cts.Token);
                return token.Wait() == MessageStatus.Replied;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 登陆超时回调
        /// </summary>
        /// <param name="state"></param>
        private void LoginTimeoutCb(object state)
        {
            if (_cts.IsCancellationRequested || Sn != "")
            {
                _loginTimer.Dispose();
                return;
            }

            _cts.Cancel();
            _logger.LogError(LogEvent.DeviceLogin, $"Device @{IpAddress}:{Port} login timeout!");
        }

        /// <summary>
        /// 心跳超时回调
        /// </summary>
        /// <param name="state"></param>
        private void HeartbeatTimeoutCb(object state)
        {
            _heartbeat?.Set();
        }

        /// <summary>
        /// 检查设备登陆请求
        /// </summary>
        /// <param name="message"></param>
        private void DeviceLogin(Inbound message)
        {
            if (Sn != "") return;
            if (!_deviceAuthorization.Authorize(message.Data))
            {
                _logger.LogError(LogEvent.DeviceLogin, $"Device @{IpAddress}:{Port} Authorization failed");
                _cts.Cancel();
                return;
            }

            Sn = Encoding.ASCII.GetString(message.Data.GetRange(0, 8).ToArray());
            var tempDeviceType = message.Data[^1];
            if (!Enum.IsDefined(typeof(DeviceType), (int) tempDeviceType))
            {
                _logger.LogError(LogEvent.DeviceLogin, $"Device @{IpAddress}:{Port} carray an invalid device-type");
                return;
            }

            Type = (Nsb.Type.DeviceType) tempDeviceType;
            using var db = new MySqlDbContext(new DbContextOptionsBuilder<MySqlDbContext>().Options);

            var de = db.Devices.FirstOrDefault(d => d.Sn == Sn);

            if (de == null)
            {
                _cts.Cancel();
                _logger.LogError(LogEvent.DeviceLogin, $"Device @{IpAddress}:{Port} carray an invalid sn");
                return;
            }

            _notificationContext.SendDeviceOnline(UserOpenId, Sn);
            if (_deviceContext.DevicePool.TryGetValue(Sn, out var device))
            {
                // 该设备已经登陆, 断开之前建立的Socket并替换为新设备
                device.Close();
                _deviceContext.DevicePool.Remove(Sn);
                _logger.LogInformation(LogEvent.DeviceLogin, $"[{Sn}]Device re-login {device.IpAddress}:{device.Port} -> {IpAddress}:{Port}");
                _deviceContext.DevicePool.Add(Sn, this);
            }
            else
            {
                _deviceContext.DevicePool.Add(Sn, this);
                _logger.LogInformation(LogEvent.DeviceLogin, $"[{Sn}]Device login @{IpAddress}:{Port}");
                _outboxQueue.TryAdd(new Outbound(Command.Login, _deviceAuthorization.GetAuthorization(Sn)));
            }
        }

        /// <summary>
        /// 注销本实例
        /// </summary>
        private void Close()
        {
            // 释放Socket资源, 通知其他任务Cancel
            _socket.Close();
            _cts.Cancel();
            _heartbeatTimer.Dispose();
            _loginTimer.Dispose();

            // 检查设备表中是否有Sn IP Socket与本机相同的设备
            if (!_deviceContext.DevicePool.TryGetValue(Sn, out var device)) return;
            if (device.Port != Port || !device.IpAddress.Equals(IpAddress)) return;
            _logger.LogInformation(LogEvent.DeviceDisconn, $"[{Sn}]Device is removed");
            _deviceContext.DevicePool.Remove(Sn);

            // 更新最后在线时间
            using var db = new MySqlDbContext(new DbContextOptionsBuilder<MySqlDbContext>().Options);
            var de = db.Devices.First(d => d.Sn == Sn);
            de.LastOnline = DateTime.Now;
            db.SaveChanges();

            // 通知前端掉线消息
            _notificationContext.SendDeviceOffline(UserOpenId, Sn);
        }
    }
}