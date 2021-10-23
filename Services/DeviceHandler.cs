using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetworkSoundBox.Hubs;
using Microsoft.AspNetCore.SignalR;
using NetworkSoundBox.Entities;
using NetworkSoundBox.Services.DTO;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace NetworkSoundBox
{
    public interface IDeviceSvrService
    {
        public List<DeviceHandler> DevicePool { get; }
    }
    public enum CMD
    {
        NONE = 0xFF,
        ACTIVATION = 0x00,
        LOGIN = 0x01,
        HEARTBEAT = 0x02,
        FILE_TRANS_REQ = 0xA0,
        FILE_TRANS_PROC = 0xA1,
        FILE_TRANS_ERR = 0xA2,
        FILE_TRANS_RPT = 0xA3
    }
    public enum MessageStatus
    {
        Untouched,
        Sending,
        Sent,
        Replied,
        Failed
    }
    public enum FileStatus
    {
        Pending,
        Success,
        Failed
    }
    public class DeviceSvrService : IDeviceSvrService
    {
        public static readonly List<DeviceHandler> MyDeviceHandles = new List<DeviceHandler>();

        List<DeviceHandler> IDeviceSvrService.DevicePool => MyDeviceHandles;
    }
    public class DeviceHandler
    {
        private readonly IHubContext<NotificationHub> _notificationHub;

        public const int MAX_RETRY_TIMES = 3;
        public const int DEFAULT_TIMEOUT_LONG = 60 * 1000;

        public string SN { get; private set; }
        public Socket Socket { get; }
        public IPAddress IPAddress { get; private set; }
        public int Port { get; private set; }
        public CancellationTokenSource CTS { get; private set;  }
        public BlockingCollection<File> FileQueue { get => _fileQueue; }
        public readonly List<MessageToken> MessageTokenList = new();

        private readonly byte[] _receiveBuffer;
        private readonly List<byte> _data;
        private readonly BlockingCollection<MessageOutbound> _outboxQueue;
        private readonly BlockingCollection<MessageInbound> _inboxQueue;
        private readonly BlockingCollection<File> _fileQueue;
        private readonly Task _handleDeviceTask;
        private readonly Task _handleInboxTask;
        private readonly Task _handleOutboxTask;
        private readonly Task _handleFilesTask;
        private int _fileCount = 0;
        private RetryManager heartbeat;

        public DeviceHandler(Socket socket, IHubContext<NotificationHub> notificationHub)
        {
            _notificationHub = notificationHub;

            Socket = socket;
            Socket.ReceiveTimeout = 1;
            Socket.SendTimeout = 5000;

            SN = "";
            IPAddress = ((IPEndPoint)socket.RemoteEndPoint).Address;
            Port = ((IPEndPoint)socket.RemoteEndPoint).Port;
            _receiveBuffer = new byte[300];
            _data = new List<byte>();

            CTS = new CancellationTokenSource();
            _outboxQueue = new BlockingCollection<MessageOutbound>(20);
            _fileQueue = new BlockingCollection<File>(20);
            _inboxQueue = new BlockingCollection<MessageInbound>(20);

            _handleDeviceTask = HandleDevice();
            _handleInboxTask = HandleInbox();
            _handleOutboxTask = HandleOutbox();
            _handleFilesTask = HandleFiles();
        }

        private void LoginTimeoutCallback(object state)
        {
            if (CTS.IsCancellationRequested) return;
            if (SN == "")
            {
                CTS.Cancel();
                Console.WriteLine("Device @{0}:{1} login timeout!", ((IPEndPoint)Socket.RemoteEndPoint).Address, ((IPEndPoint)Socket.RemoteEndPoint).Port);
            }
        }
        private void HeartbeatTimeoutCallback(object state)
        {
            if (heartbeat != null)
            {
                heartbeat.Set();
            }
        }
        private void ParseMessage(List<byte> data)
        {
            //Console.WriteLine("[{0}] Received {1} bytes message", SN == "" ? $"@{IPAddress}:{Port}" : $"{SN}", data.Count);
            while (true)
            {
                int startOffset = data.IndexOf(0x7E);
                if (startOffset < 0)
                {
                    break;
                }
                //!!长度检查
                int endOffset = FindEnd(startOffset, data);
                if (data[endOffset] == 0xEF && Enum.IsDefined(typeof(CMD), (int)data[startOffset + 1]))
                {
                    try
                    {
                        Console.WriteLine("[{0}] Recv [{1}] Bytes CMD[0x{2:X2}]", SN == "" ? $"@{IPAddress}:{Port}" : $"{SN}", data.Count, data[startOffset + 1]);
                        _inboxQueue.Add(new MessageInbound(data.Skip(startOffset).Take(endOffset - startOffset + 1).ToList()));
                    }
                    catch (OperationCanceledException)
                    {
                        data.RemoveRange(0, data.Count);
                        return;
                    }
                    data.RemoveRange(0, endOffset + 1);
                }
                else
                {
                    data.RemoveRange(0, startOffset + 1);
                }
            }
            data.RemoveRange(0, data.Count);
        }
        private static int FindEnd(int startOffset, List<byte> data)
        {
            int messageLen = data[startOffset + 2] | data[startOffset + 3];
            return startOffset + messageLen + 4 < data.Count ? startOffset + messageLen + 4 : startOffset;
        }
        private MessageToken GetToken(CMD command)
        {
            if (command != CMD.NONE)
                return MessageTokenList.FirstOrDefault(token => token.ExpectCommand == command || token.ExceptionCommand == command);
            return null;
        }
        private async Task HandleDevice()
        {
            await Task.Yield();
            Console.WriteLine("Device @{0}:{1} has connected!", IPAddress, Port);
            Timer loginTimeout = new(new TimerCallback(LoginTimeoutCallback), this, 10000, Timeout.Infinite);
            heartbeat = new(1, new(o => CTS.Cancel()));
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
                    if (DeviceSvrService.MyDeviceHandles.Contains(this))
                    {
                        DeviceSvrService.MyDeviceHandles.Remove(this);
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
                        await _notificationHub.Clients.All.SendAsync(
                            NotificationHub.NOTI_LOGOUT,
                            JsonConvert.SerializeObject(new ConnectionNotifyDto
                            {
                                Sn = SN,
                                LastOnline = logoutTime
                            }));
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
                                ParseMessage(_data);
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
        private async Task HandleInbox()
        {
            await Task.Yield();
            MessageInbound message = null;
            while(true)
            {
                message = null;
                try
                {
                    message = _inboxQueue.Take(CTS.Token);
                    MessageToken token = GetToken(message.Command);
                    switch (message.Command)
                    {
                        case CMD.LOGIN:
                            if (SN == "")
                            {
                                long timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                                if (timeStamp % 10 < 5)
                                {
                                    timeStamp -= timeStamp % 10;
                                }
                                else
                                {
                                    timeStamp += (10 - timeStamp % 10);
                                }
                                var timeStampStr = timeStamp.ToString();
                                SN = Encoding.ASCII.GetString(message.Data.GetRange(0, 8).ToArray());
                                var tokenStr = Encoding.ASCII.GetString(message.Data.GetRange(8, message.Data.Count - 8).ToArray());

                                using var hmacmd5_recv_1 = new HMACMD5(Encoding.ASCII.GetBytes("hengliyuan123"));
                                var keyBytes = hmacmd5_recv_1.ComputeHash(Encoding.ASCII.GetBytes(SN));
                                var keyStr = "";
                                new List<byte>(keyBytes).ForEach(b =>
                                {
                                    keyStr += b.ToString("x2");
                                });
                                using var hmacmd5_recv_2 = new HMACMD5(Encoding.ASCII.GetBytes(keyStr));
                                keyBytes = hmacmd5_recv_2.ComputeHash(Encoding.ASCII.GetBytes(timeStampStr));
                                keyStr = "";
                                new List<byte>(keyBytes).ForEach(b =>
                                {
                                    keyStr += b.ToString("x2");
                                });

                                if (tokenStr != keyStr)
                                {
                                    Console.WriteLine("Authorization failed. Device sends {0} while it should be {1}", tokenStr, keyStr);
                                    CTS.Cancel();
                                    break;
                                }

                                using (MySqlDbContext dbContext = new(new DbContextOptionsBuilder<MySqlDbContext>().Options))
                                {
                                    var deviceEntity = dbContext.Devices.FirstOrDefault(d => d.Sn == SN);
                                    if (deviceEntity == null)
                                    {
                                        CTS.Cancel();
                                        Console.WriteLine("Invalid SN \"{0}\", socket @{1}:{2} will be closed!",
                                            SN,
                                            IPAddress,
                                            Port);
                                        break;
                                    }
                                    else
                                    {
                                        await _notificationHub.Clients.All.SendAsync(
                                            NotificationHub.NOTI_LOGIN,
                                            JsonConvert.SerializeObject(new ConnectionNotifyDto
                                            {
                                                Sn = SN
                                            }));
                                    }
                                }
                                DeviceHandler device = DeviceSvrService.MyDeviceHandles.FirstOrDefault(device => device.SN == SN);
                                if (device != null)
                                {
                                    device.CTS.Cancel();
                                    DeviceSvrService.MyDeviceHandles.Remove(device);
                                    Console.WriteLine("Device with SN \"{0}\" has existed, renew device", SN);
                                }
                                DeviceSvrService.MyDeviceHandles.Add(this);
                                Console.WriteLine("Device with SN \"{0}\" has loged in, socket @{1}:{2}. Now we have got {3} devices.",
                                        SN,
                                        IPAddress,
                                        Port,
                                        DeviceSvrService.MyDeviceHandles.Count);

                                using var hmacmd5_send_1 = new HMACMD5(Encoding.ASCII.GetBytes("abcdefg"));
                                var authBytes = hmacmd5_send_1.ComputeHash(Encoding.ASCII.GetBytes(SN));
                                var authStr = "";
                                new List<byte>(authBytes).ForEach(b =>
                                {
                                    authStr += b.ToString("x2");
                                });
                                using var hmacmd5_send_2 = new HMACMD5(Encoding.ASCII.GetBytes(authStr));

                                timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                                if (timeStamp % 10 < 5)
                                {
                                    timeStamp -= timeStamp % 10;
                                }
                                else
                                {
                                    timeStamp += (10 - timeStamp % 10);
                                }
                                timeStampStr = timeStamp.ToString();

                                authBytes = hmacmd5_send_2.ComputeHash(Encoding.ASCII.GetBytes(timeStampStr));
                                authStr = "";
                                new List<byte>(authBytes).ForEach(b =>
                                {
                                    authStr += b.ToString("x2");
                                });
                                _outboxQueue.TryAdd(new MessageOutbound(CMD.LOGIN, Encoding.ASCII.GetBytes(authStr)));
                            }
                            break;
                        case CMD.FILE_TRANS_REQ:
                            if (token != null)
                            {
                                token.Data = message.Data.ToArray();
                                token.Status = MessageStatus.Replied;
                            }
                            break;
                        case CMD.FILE_TRANS_ERR:
                            if (token != null)
                            {
                                token.Data = message.Data.ToArray();
                                token.Status = MessageStatus.Failed;
                            }
                            break;
                        case CMD.FILE_TRANS_RPT:
                            if (token != null)
                            {
                                token.Data = message.Data.ToArray();
                                token.Status = MessageStatus.Replied;
                            }
                            break;
                        case CMD.HEARTBEAT:
                            Console.WriteLine("收到心跳信号");
                            _outboxQueue.Add(new MessageOutbound(CMD.HEARTBEAT));
                            break;
                        default:
                            break;
                    }
                    if (heartbeat != null)
                    {
                        heartbeat.Reset();
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Exit HandleInbox() Task");
                    return;
                }
            }
        }
        private async Task HandleOutbox()
        {
            await Task.Yield();
            MessageOutbound message = null;
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
                                case CMD.LOGIN:
                                    Socket.Send(message.MessageBuffer, message.BufferSize, 0);
                                    message.Token.Status = MessageStatus.Sent;
                                    break;
                                case CMD.FILE_TRANS_REQ:
                                case CMD.FILE_TRANS_PROC:
                                case CMD.FILE_TRANS_RPT:
                                    Socket.Send(message.MessageBuffer, message.BufferSize, 0);
                                    message.Token.Status = MessageStatus.Sent;
                                    break;
                                case CMD.HEARTBEAT:
                                    Socket.Send(message.MessageBuffer, message.BufferSize, 0);
                                    message.Token.Status = MessageStatus.Sent;
                                    break;
                                default:
                                    throw new ArgumentException();
                            }
                            Console.WriteLine("[{0}] Trns [{1}] Bytes CMD[0x{2:X2}]", SN, message.BufferSize, (int)message.Command);
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

                        MessageToken token = new(
                            MessageTokenList,
                            CMD.FILE_TRANS_REQ,//期待回复命令
                            CMD.FILE_TRANS_ERR,//错误回报命令
                            new byte[] { 0x00, 0x00 });//期待回复数据

                        _outboxQueue.Add(new(
                            CMD.FILE_TRANS_REQ,//待发送命令
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
                        await _notificationHub.Clients.All.SendAsync("FileProgress", (100.0 * pkgIdx / file.PackageCount).ToString());
                        retry.Reset();
                        while (retry.Set())
                        {

                            Console.WriteLine("[{0}] Transmitting {1}/{2} package of file[{3}], retry:{4}", SN, pkgIdx, file.PackageCount, _fileCount + 1, retry.Count);

                            MessageToken token = new(
                                MessageTokenList,
                                CMD.FILE_TRANS_REQ,//期待回复命令
                                CMD.FILE_TRANS_ERR,//错误回报命令
                                new byte[] { (byte)(pkgIdx >> 8), (byte)pkgIdx });//期待回复数据
                            
                            _outboxQueue.Add(new(
                                CMD.FILE_TRANS_PROC,//待发送命令
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

                        MessageToken token = new(
                            MessageTokenList,
                            CMD.FILE_TRANS_RPT,
                            CMD.FILE_TRANS_ERR,
                            new byte[] { 0x00, (byte)(_fileCount + 1) });

                        _outboxQueue.Add(new MessageOutbound(
                            CMD.FILE_TRANS_RPT,
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

                            MessageToken token = new(
                                MessageTokenList,
                                CMD.FILE_TRANS_RPT,
                                CMD.FILE_TRANS_RPT,
                                new byte[] { 0x00, 0x00 });

                            _outboxQueue.Add(new MessageOutbound(
                                CMD.FILE_TRANS_RPT,
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
                    while(_fileQueue.Count != 0)
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
                    while(_fileQueue.Count != 0)
                    {
                        _fileQueue.Take().Fail();
                    }
                    return; 
                }
                catch (Exception ex) { Console.WriteLine(ex.Message); }
            }
        }
    }
    public class ServerService : BackgroundService
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public ServerService(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        protected async override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            IPAddress listeningAddr = IPAddress.Parse("0.0.0.0");
            TcpListener server = new(listeningAddr, 10808);
            server.Start();
            while (true)
            {
                Console.WriteLine("[Test] Waiting for a connection...");
                new DeviceHandler(server.AcceptSocket(), _hubContext);
            }
        }
    }
    public class MessageInbound
    {
        public CMD Command { get; }
        public int MessageLen { get; }
        public List<byte> Data { get; }

        public MessageInbound(List<byte> message)
        {
            Command = (CMD)message[1];
            MessageLen = message[2] | message[3];
            Data = message.Skip(4).Take(MessageLen).ToList();
        }
    }
    public class MessageOutbound
    {
        private const byte STARTBYTE = 0x7E;
        private const byte ENDBYTE = 0xEF;
        private readonly int _dataLen;

        public CMD Command { get; }
        public List<byte> ParamList { get; }
        public int BufferSize { get; }
        public byte[] MessageBuffer { get; } = new byte[300];
        public MessageToken Token { get; }

        public MessageOutbound(CMD command, MessageToken token = null, params byte[] param)
        {
            Command = command;
            ParamList = new List<byte>(param);
            _dataLen = ParamList.Count;
            Token = token ?? new(null,null, null);

            int offset = 0;
            MessageBuffer[offset++] = STARTBYTE;
            MessageBuffer[offset++] = (byte)Command;
            MessageBuffer[offset++] = (byte)(_dataLen >> 8);
            MessageBuffer[offset++] = (byte)_dataLen;
            ParamList.ForEach(param => MessageBuffer[offset++] = param);
            MessageBuffer[offset++] = ENDBYTE;
            BufferSize = offset;
        }

        public MessageOutbound(CMD command, int packageIndex, MessageToken token = null, params byte[] param)
        {
            Command = command;
            ParamList = new List<byte>(param);
            _dataLen = ParamList.Count;
            Token = token ?? new(null,null, null);

            int offset = 0;
            MessageBuffer[offset++] = STARTBYTE;
            MessageBuffer[offset++] = (byte)Command;
            MessageBuffer[offset++] = (byte)(packageIndex >> 8);
            MessageBuffer[offset++] = (byte)packageIndex;
            ParamList.ForEach(param => MessageBuffer[offset++] = param);
            MessageBuffer[offset++] = ENDBYTE;
            BufferSize = offset;
        }

        public MessageOutbound(CMD command, params byte[] param)
        {
            Command = command;
            ParamList = new List<byte>(param);
            _dataLen = ParamList.Count;
            Token = new(null,null, null);

            int offset = 0;
            MessageBuffer[offset++] = STARTBYTE;
            MessageBuffer[offset++] = (byte)Command;
            MessageBuffer[offset++] = (byte)(_dataLen >> 8);
            MessageBuffer[offset++] = (byte)_dataLen;
            ParamList.ForEach(param => MessageBuffer[offset++] = param);
            MessageBuffer[offset++] = ENDBYTE;
            BufferSize = offset;
        }
    }
    public class MessageToken
    {
        public readonly List<MessageToken> MessageTokenList;
        public byte[] Data { get; set; }
        private readonly Semaphore _semaphore;
        public CMD ExpectCommand { get; }
        public CMD ExceptionCommand { get; }
        private MessageStatus _status;
        public MessageStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                if (_semaphore != null)
                {
                    _semaphore.Release();
                    if (_status >= MessageStatus.Replied && MessageTokenList.Contains(this))
                        MessageTokenList.Remove(this);
                }
            }
        }
        private readonly byte[] _expectReply;

        public MessageToken(List<MessageToken> messageTokenList, CMD? expectCommand, CMD? exceptionCommand, byte[] expectReply = null)
        {
            MessageTokenList = messageTokenList;
            _status = MessageStatus.Untouched;
            _semaphore = new Semaphore(0, 3);
            ExpectCommand = expectCommand ?? CMD.NONE;
            ExceptionCommand = exceptionCommand ?? CMD.NONE;
            _expectReply = expectReply;
            if (ExceptionCommand != CMD.NONE || ExpectCommand != CMD.NONE)
                MessageTokenList.Add(this);
        }

        public bool CheckReply(byte[] expectReply = null)
        {
            expectReply ??= _expectReply;
            if (Data == null || expectReply == null) return false;

            if (Data.Length != expectReply.Length) return false;

            for (int i = 0; i < Data.Length; i++)
                if (Data[i] != expectReply[i]) return false;

            return true;
        }

        public void WaitSent(RetryManager retry = null)
        {
            retry ??= new();
            do
            {
                if (!_semaphore.WaitOne(retry.Timeout))
                    retry.Set();
                if (_status == MessageStatus.Failed)
                    retry.Trigger();
            } while (_status != MessageStatus.Sent);
        }

        public void WaitReplied(RetryManager retry = null)
        {
            retry ??= new();
            do
            {
                if (!_semaphore.WaitOne(retry.Timeout))
                    retry.Set();
                if (_status == MessageStatus.Failed)
                    retry.Trigger();
            } while (_status != MessageStatus.Replied && retry.Set());
        }

        
    }
    public class File
    {
        private readonly List<byte> _content;
        public Semaphore Semaphore { get; } = new Semaphore(0, 1);
        public FileStatus FileStatus { get; set; }
        public Queue<byte[]> Packages { get; }
        public int PackageCount { get; }

        public File(List<byte> content)
        {
            FileStatus = FileStatus.Pending;
            _content = content;
            Packages = new Queue<byte[]>();
            PackageCount = _content.Count / 256 + 1;
            for (int index = 0; index < PackageCount; index++)
            {
                byte[] package = new byte[256];
                int bytesCopied = index * 255;
                int bytesRemain = _content.Count - bytesCopied;
                _content.CopyTo(bytesCopied, package, 0, bytesRemain > 255 ? 255 : bytesRemain);
                for (int i = 0; i < 255; i++)
                {
                    package[255] += package[i];
                }
                Packages.Enqueue(package);
            }
        }

        public void Success()
        {
            FileStatus = FileStatus.Success;
            Semaphore.Release();
        }

        public void Fail()
        {
            FileStatus = FileStatus.Failed;
            Semaphore.Release();
        }
    }
    public class RetryManager
    {
        public delegate void OverflowCallBack(params object[] args);
        protected int _maxRetryTimes;
        public int Timeout { get; } = DeviceHandler.DEFAULT_TIMEOUT_LONG;
        public int Count { get; private set; } = 0;
        private readonly OverflowCallBack _overflowCallBack;
        private readonly object[] _callbackArgs;
        private readonly Stack<int> _countStack = new();

        public RetryManager(int maxRetryTimes = DeviceHandler.MAX_RETRY_TIMES, OverflowCallBack callBack = null, params object[] args)
        {
            _maxRetryTimes = maxRetryTimes;
            _overflowCallBack = callBack ??= new OverflowCallBack((o) => throw new TimeoutException());
            _callbackArgs = args;
        }

        public bool Set()
        {
            if (++Count > _maxRetryTimes)
            {
                _overflowCallBack(_callbackArgs);
                return false;
            }
            return true;
        }

        public void Reset()
        {
            Count = 0;
        }

        public void Trigger()
        {
            Count = _maxRetryTimes;
            _countStack.Clear();
        }

        public void Save()
        {
            _countStack.Push(Count);
        }

        public void Restore()
        {
            Count = _countStack.Pop();
        }
    }
}
