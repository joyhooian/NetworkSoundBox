using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkSoundBox
{
    public interface IDeviceSvrService
    {
        public List<MyDeviceHandle> MyDeviceHandles { get; }
    }

    public class DeviceSvrService : IDeviceSvrService
    {
        public static readonly List<MyDeviceHandle> MyDeviceHandles = new List<MyDeviceHandle>();

        List<MyDeviceHandle> IDeviceSvrService.MyDeviceHandles => MyDeviceHandles;
    }

    public class MyDeviceHandle
    {
        private static readonly int MAX_RETRY_TIMES = 3;
        private Queue<ReplyMessage> OutboxQueue { get; }
        private Semaphore OutboxSem { get; }
        public CancellationTokenSource CancellationTokenSource { get; }
        public Socket Socket { get; }
        public IPAddress IPAddress { get; }
        public int Port { get; }
        private List<byte> Data { get; }
        private byte[] receiveBuffer { get; }
        private Queue<File> Files { get; }
        private Semaphore FilesSem { get; }
        private string SN { get; set; }
        private Semaphore ProcessSem { get; } 

        public MyDeviceHandle(Socket socket)
        {
            OutboxQueue = new Queue<ReplyMessage>();
            OutboxSem = new Semaphore(0, 100);
            CancellationTokenSource = new CancellationTokenSource();
            Socket = socket;
            IPAddress = ((IPEndPoint)socket.RemoteEndPoint).Address;
            Port = ((IPEndPoint)socket.RemoteEndPoint).Port;
            SN = "";
            Socket.ReceiveTimeout = 200;
            Socket.SendTimeout = 5000;
            Data = new List<byte>();
            receiveBuffer = new byte[300];
            Files = new Queue<File>();

            ProcessSem = new Semaphore(1, 1);
            var outboxTask = OutboxTask();
            var task = DeviceHandleTask();
        }

        private void LoginTimeoutCallback(object state)
        {
            if (CancellationTokenSource.IsCancellationRequested)
            {
                return;
            }
            if (SN == "")
            {
                CancellationTokenSource.Cancel();
                Console.WriteLine("Device @{0}:{1} login timeout!", ((IPEndPoint)Socket.RemoteEndPoint).Address, ((IPEndPoint)Socket.RemoteEndPoint).Port);
            }
        }

        private void ParseMessage(List<byte> data)
        {
            Console.WriteLine("[{0}] Received {1} bytes message", SN == "" ? $"@{IPAddress}:{Port}" : $"{SN}", data.Count);
            while (true)
            {
                int startOffset = data.IndexOf(0x7E);
                if (startOffset < 0)
                {
                    break;
                }
                int endOffset = FindEnd(startOffset, data);
                if (data[endOffset] == 0xEF && Enum.IsDefined(typeof(CMD), (int)data[startOffset + 1]))
                {
                    var p = HandleMessage(new MyMessage(data.Skip(startOffset).Take(endOffset - startOffset + 1).ToList()));
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
            return messageLen + 4 < data.Count ? messageLen + 4 : startOffset;
        }

        private async Task DeviceHandleTask()
        {
            await Task.Yield();
            Console.WriteLine("Device @{0}:{1} has connected!", IPAddress, Port);
            Timer loginTimeout = new Timer(new TimerCallback(LoginTimeoutCallback), this, 10000, Timeout.Infinite);
            int receiveCount = 0;
            while (true)
            {
                if (CancellationTokenSource.IsCancellationRequested)
                {
                    Console.WriteLine("Device {2}@{0}:{1} has disconnected!", IPAddress, Port, SN == "" ? "" : $"[{SN}]");
                    Socket.Close();
                    if (DeviceSvrService.MyDeviceHandles.Contains(this))
                    {
                        DeviceSvrService.MyDeviceHandles.Remove(this);
                    }
                    break;
                }
                try
                {
                    receiveCount = Socket.Receive(receiveBuffer);
                    if (receiveCount == 0)
                    {
                        CancellationTokenSource.Cancel();
                        break;
                    }
                    else
                    {
                        for (int cnt = 0; cnt < receiveCount; cnt++)
                        {
                            Data.Add(receiveBuffer[cnt]);
                        }
                    }
                }
                catch (SocketException ex)
                {
                    switch (ex.SocketErrorCode)
                    {
                        case SocketError.TimedOut:
                            if (Data.Count != 0)
                            {
                                ParseMessage(Data);
                                Data.Clear();
                            }
                            break;
                        case SocketError.ConnectionReset:
                            CancellationTokenSource.Cancel();
                            break;
                        default:
                            Console.WriteLine(ex.Message + "Error Code: {0}", ex.ErrorCode);
                            break;

                    }
                }
            }
        }

        private async Task HandleMessage(MyMessage message)
        {
            await Task.Yield();
            ProcessSem.WaitOne();
            Console.WriteLine("Entry Process()");
            switch (message.Command)
            {
                case CMD.LOGIN:
                    if (SN == "")
                    {
                        SN = System.Text.Encoding.ASCII.GetString(message.Data.ToArray());
                        using (MySqlDbContext dbContext = new(new DbContextOptionsBuilder<MySqlDbContext>().Options))
                        {
                            if (null == dbContext.Device.FirstOrDefault(device => device.sn == SN))
                            {
                                CancellationTokenSource.Cancel();
                                Console.WriteLine("Invalid SN \"{0}\", socket @{1}:{2} will be closed!",
                                    SN,
                                    IPAddress,
                                    Port);
                                break;
                            }
                        }
                        if (null != DeviceSvrService.MyDeviceHandles.FirstOrDefault(device => device.SN == SN))
                        {
                            CancellationTokenSource.Cancel();
                            Console.WriteLine("Device with SN \"{0}\" has existed, socket @{1}:{2} will be closed!",
                                SN,
                                IPAddress,
                                Port);
                            break;
                        }
                        DeviceSvrService.MyDeviceHandles.Add(this);
                        Console.WriteLine("Device with SN \"{0}\" has loged in, socket @{1}:{2}. Now we have got {3} devices.",
                                SN,
                                IPAddress,
                                Port,
                                DeviceSvrService.MyDeviceHandles.Count);
                    }
                    OutboxQueue.Enqueue(new ReplyMessage(CMD.LOGIN));
                    OutboxSem.Release();
                    break;
                default:
                    break;
            }
            Console.WriteLine("Leaving Process()");
            ProcessSem.Release();
        }

        private async Task OutboxTask()
        {
            await Task.Yield();
            int retryTimes;
            while(true)
            {
                OutboxSem.WaitOne(1000);
                if (OutboxQueue.Count > 0)
                {
                    ReplyMessage message = OutboxQueue.Dequeue();
                    retryTimes = 0;
                    while (retryTimes < MAX_RETRY_TIMES)
                    {
                        if (CancellationTokenSource.IsCancellationRequested)
                        {
                            return;
                        }
                        try
                        {
                            switch (message.Command)
                            {
                                case CMD.LOGIN:
                                    Socket.Send(new byte[] { 0x7E, 0x01, 0x00, 0x00, 0xEF });
                                    retryTimes = MAX_RETRY_TIMES;
                                    break;
                                default:
                                    break;
                            }
                        }
                        catch(SocketException ex)
                        {
                            if (ex.SocketErrorCode == SocketError.TimedOut)
                            {
                                retryTimes++;
                            }
                            else
                            {
                                Console.WriteLine(ex.Message);
                            }
                        }
                    }
                }
            }
        }

        private async Task HandleFiles()
        {
            await Task.Yield();
            while(true)
            {

            }
        }
    }

    public class MyMessage
    {
        public CMD Command { get; }
        public int MessageLen { get; }
        public List<byte> Data { get; }

        public MyMessage(List<byte> message)
        {
            Command = (CMD)message[1];
            MessageLen = message[2] | message[3];
            Data = message.Skip(4).Take(MessageLen).ToList();
        }
    }

    public class ReplyMessage
    {
        public CMD Command { get; }

        public ReplyMessage(CMD command)
        {
            Command = command;
        }
    }

    public class File
    {
        public List<byte> Content { get; }
        public int FileIndex { get; }
        public File(List<byte> content, int fileIndex)
        {
            Content = content;
            FileIndex = FileIndex;
        }
    }

    public class ServerService : BackgroundService
    {
        protected async override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            IPAddress listeningAddr = IPAddress.Parse("0.0.0.0");
            TcpListener server = new TcpListener(listeningAddr, 10809);
            server.Start();
            while (true)
            {
                Console.WriteLine("[Test] Waiting for a connection...");
                new MyDeviceHandle(server.AcceptSocket());
            }
        }
    }

    //public class ProcessMessageService : BackgroundService
    //{
    //    protected async override Task ExecuteAsync(CancellationToken cancellationToken)
    //    {
    //        await Task.Yield();
    //        while (true)
    //        {
    //            while (true)
    //            {
    //                MyTcpService.ProcessMessageSem.WaitOne();
    //                if (MyTcpService.ProcessMessages.Count != 0) { break; }
    //            }
    //            MyMessage message = MyTcpService.ProcessMessages.Dequeue();
    //            switch (message.Command)
    //            {
    //                case CMD.LOGIN:
    //                    if (message.DeviceHandle.SN == "")
    //                    {
    //                        string sn = System.Text.Encoding.ASCII.GetString(message.Data.ToArray());
    //                        using (MySqlDbContext dbContext = new(new DbContextOptionsBuilder<MySqlDbContext>().Options))
    //                        {
    //                            if (null == dbContext.Device.FirstOrDefault(device => device.sn == sn))
    //                            {
    //                                message.DeviceHandle.CancellationTokenSource.Cancel();
    //                                Console.WriteLine("Invalid SN \"{0}\", socket @{1}:{2} will be closed!",
    //                                    sn,
    //                                    ((IPEndPoint)message.DeviceHandle.Socket.RemoteEndPoint).Address,
    //                                    ((IPEndPoint)message.DeviceHandle.Socket.RemoteEndPoint).Port);
    //                                continue;
    //                            }
    //                        }
    //                        if (null != MyTcpService.MyDeviceHandles.FirstOrDefault(device => device.SN == sn))
    //                        {
    //                            message.DeviceHandle.CancellationTokenSource.Cancel();
    //                            Console.WriteLine("Device with SN \"{0}\" has existed, socket @{1}:{2} will be closed!",
    //                                sn,
    //                                ((IPEndPoint)message.DeviceHandle.Socket.RemoteEndPoint).Address,
    //                                ((IPEndPoint)message.DeviceHandle.Socket.RemoteEndPoint).Port);
    //                            continue;
    //                        }
    //                        message.DeviceHandle.SN = sn;
    //                        MyTcpService.MyDeviceHandles.Add(message.DeviceHandle);
    //                        Console.WriteLine("Device with SN \"{0}\" has loged in, socket @{1}:{2}. Now we have got {3} devices.",
    //                                sn,
    //                                ((IPEndPoint)message.DeviceHandle.Socket.RemoteEndPoint).Address,
    //                                ((IPEndPoint)message.DeviceHandle.Socket.RemoteEndPoint).Port,
    //                                MyTcpService.MyDeviceHandles.Count);
    //                    }
    //                    var reply = message.DeviceHandle.Reply(CMD.LOGIN);
    //                    break;
    //                default:
    //                    break;
    //            }
    //        }
    //    }
    ////}
}
