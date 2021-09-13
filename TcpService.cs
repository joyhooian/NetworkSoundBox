using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using NetworkSoundBox.Models;
using Microsoft.Extensions.DependencyInjection;

namespace NetworkSoundBox
{
    public interface ITcpService
    {
        public Queue<Message> MessageQueue { get; }
        public List<DeviceHandle> DevicePool { get; }
    }

    public enum DeviceType
    {
        NONE,
        TEST
    }

    public enum CMD
    {
        ACTIVATION  =   0x00,
        LOGIN       =   0x01,
        HEARTBEAT   =   0x02
    }

    public class DeviceHandle
    {
        public string SN { get; set; }
        public DeviceType DeviceType { get; set; }
        public TcpClient Client { get; }
        public string IPAddress { get; }
        public string Port { get; }
        public CancellationTokenSource CTS { get; }

        public DeviceHandle(TcpClient tcpClient, CancellationTokenSource cancellationTokenSource)
        {
            SN = "";
            DeviceType = DeviceType.NONE;
            Client = tcpClient;
            CTS = cancellationTokenSource;
            IPAddress = ((IPEndPoint)Client.Client.RemoteEndPoint).Address.ToString();
            Port = ((IPEndPoint)Client.Client.RemoteEndPoint).Port.ToString();
            Console.WriteLine("Device connected with IP: {0} on port {1}", IPAddress, Port);
        }
    }

    public class Message
    {
        public List<byte> RawBytes { get; }
        public int MessageLen { get; }
        public CMD CMD { get; }
        public List<byte> DataList { get; }
        public DeviceHandle DeviceHandle { get; }

        public Message(List<byte> list, DeviceHandle deviceHandle)
        {
            RawBytes = new List<byte>(list);
            DeviceHandle = deviceHandle;

            int index = 1;
            MessageLen = list[index++];
            CMD = (CMD)list[index++];
            if (index < list.Count - 1)
            {
                DataList = RawBytes.Skip(index).Take(list.Count - index - 1).ToList();
            }
        }
    }

    public class TcpService : ITcpService
    {
        static List<DeviceHandle> DevicePool = null;
        static Queue<Message> MessageQueue = null;
        static Semaphore MessageQueueSem = null;
        static Thread MessageQueueTask = null;
        static Thread TcpListenerTask = null;
        private readonly MySqlDbContext _dbContext;
        public TcpService(MySqlDbContext dbContext)
        {
            _dbContext = dbContext;
            if (DevicePool == null)
            {
                DevicePool = new List<DeviceHandle>();
            }
            if (MessageQueue == null)
            {
                MessageQueue = new Queue<Message>();
            }
            if (MessageQueueSem == null)
            {
                MessageQueueSem = new Semaphore(0, 100, "messageQueueSem");
            }
            if (MessageQueueTask == null)
            {
                MessageQueueTask = new Thread(PerseMessage);
            }
            if (TcpListenerTask == null)
            {
                TcpListenerTask = new Thread(TcpListener);
            }
            if (!TcpListenerTask.IsAlive)
            {
                TcpListenerTask.Start();
            }
            if (!MessageQueueTask.IsAlive)
            {
                MessageQueueTask.Start();
            }
        }

        Queue<Message> ITcpService.MessageQueue
        {
            get { return MessageQueue; }
        }

        List<DeviceHandle> ITcpService.DevicePool
        {
            get { return DevicePool; }
        }

        static private void TcpListener()
        {
            IPAddress localAddr = IPAddress.Parse("0.0.0.0");
            TcpListener server = new TcpListener(localAddr, 10808);
            server.Start();
            while (true)
            {
                Console.WriteLine("Waiting for a connection...");
                DeviceHandle deviceHandle = new DeviceHandle(server.AcceptTcpClient(), new CancellationTokenSource());
                Thread thread = new Thread(TcpClientThread);
                thread.Start(deviceHandle);
            }
        }

        static private void TcpClientThread(object deviceHandle)
        {
            DeviceHandle _deviceHandle = (DeviceHandle)deviceHandle;
            Socket socket = _deviceHandle.Client.Client;
            socket.ReceiveTimeout = 200;
            byte[] bytes = new byte[512];
            List<byte> data = new List<byte>();
            int receiveCount = 0;

            while (true)
            {
                if (_deviceHandle.CTS.Token.IsCancellationRequested)
                {
                    _deviceHandle.Client.Close();
                    _deviceHandle.Client.Dispose();
                    _deviceHandle.CTS.Dispose();
                    Console.WriteLine("Device connection with IP: {0} has disconnected!", _deviceHandle.IPAddress);

                    if (DevicePool.Contains(_deviceHandle))
                    {
                        var _dbContext = Startup._services.BuildServiceProvider().GetService<MySqlDbContext>();
                        if (_dbContext != null)
                        {
                            try
                            {
                                Device device = _dbContext.Device.Where(device => device.sn == _deviceHandle.SN).FirstOrDefault();
                                if (device != null)
                                {
                                    device.lastOnline = DateTime.Now;
                                    _dbContext.Update(device);
                                    _dbContext.SaveChanges();
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                            }
                        }
                        DevicePool.Remove(_deviceHandle);
                    }

                    break;
                }
                try
                {
                    receiveCount = socket.Receive(bytes);
                    if (receiveCount == 0)
                    {
                        _deviceHandle.CTS.Cancel();
                    }
                    for (int i = 0; i < receiveCount; i++)
                    {
                        data.Add(bytes[i]);
                    }
                }
                catch (Exception)
                {
                    if (data.Count != 0)
                    {
                        ReceiveMessage(data, _deviceHandle);
                    }
                }
            }
        }

        static private void ReceiveMessage(List<byte> message, DeviceHandle deviceHandle)
        {
            Console.WriteLine("Received {0} bytes message", message.Count);
            while ( message.IndexOf(0x7E) < message.IndexOf(0xEF) )
            {
                List<byte> frame = message
                    .Skip(message.FindIndex(b => b == 0x7E))
                    .TakeWhile(b => b != 0xEF)
                    .ToList();
                frame.Add(0xEF);
                if (frame.Count > 3 && frame.Count == frame[1] + 2)
                {
                    MessageQueue.Enqueue(new Message(frame, deviceHandle));
                    MessageQueueSem.Release();
                    frame.ForEach(b => Console.Write("0x{0:X} ", b));
                    Console.WriteLine();
                }
                message.RemoveRange(message.IndexOf(0x7E), frame.Count);
            }
            message.RemoveRange(0, message.Count);
        }

        static private void PerseMessage()
        {
            Message message;
            DeviceHandle deviceHandle;
            while(true)
            {
                MessageQueueSem.WaitOne();
                message = MessageQueue.Dequeue();
                deviceHandle = message.DeviceHandle;

                if (message.DeviceHandle.SN == "")
                {
                    //检查是否登陆
                    if (message.CMD == CMD.LOGIN)
                    {
                        deviceHandle.SN = System.Text.Encoding.ASCII.GetString(message.DataList.ToArray());
                        deviceHandle.DeviceType = DeviceType.TEST;
                        if (!DevicePool.Contains(deviceHandle))
                        {
                            DevicePool.Add(deviceHandle);
                        }
                        deviceHandle.Client.Client.Send(new byte[] { 0x7E, 0x02, 0x01, 0xEF });
                    }
                    else
                    {
                        Console.WriteLine("Device is unloged, disconnect!");
                        deviceHandle.CTS.Cancel();
                    }
                }
                else
                {
                    switch(message.CMD)
                    {
                        case CMD.ACTIVATION:
                            break;
                        case CMD.LOGIN:
                            deviceHandle.Client.Client.Send(new byte[] { 0x7E, 0x02, 0x01, 0xEF });
                            break;
                        case CMD.HEARTBEAT:
                            deviceHandle.Client.Client.Send(new byte[] { 0x7E, 0x02, 0x02, 0xEF });
                            break;
                        default:
                            break;
                    }
                }
            }
        }
    }
}
