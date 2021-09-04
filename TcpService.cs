using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Net;

namespace NetworkSoundBox
{
    public interface ITcpService
    {
        List<Thread> ThreadPool { get; }
        Queue<Message> MessageQueue { get; }
        Dictionary<int, DeviceHandle> DevicePool { get; }
    }

    public enum DeviceType
    {
        TEST_DEVICE
    }

    public class DeviceHandle
    {
        private string _sn;
        public string SN { get; set; }

        private DeviceType _deviceType;
        public DeviceType DeviceType { get; set; }

        private TcpClient _client;
        public TcpClient TcpClient { get; set; }

        private Thread _threadHandle;
        public Thread Thread { get; set; }

        public DeviceHandle(TcpClient tcpClient, Thread threadHandle)
        {

            _client = tcpClient;
            _threadHandle = threadHandle;
        }
    }

    public class Message
    {
        private List<byte> _rawBytes;
        public List<byte> RawBytes { get; }

        private DeviceHandle _deviceHandle;
        public DeviceHandle DeviceHandle { get; }

        public Message(List<byte> list)
        {
            _rawBytes = new List<byte>(list);
        }

        public Message(List<byte> list, DeviceHandle deviceHandle)
        {
            _rawBytes = new List<byte>(list);
            _deviceHandle = deviceHandle;
        }
    }

    public class TCPService : ITcpService
    {
        static List<Thread> _threadPool = null;
        static Queue<Message> _messagesQueue = null;
        static Thread _tcpListenerThread = null;
        static Semaphore _messageQueueSem = null;
        static Dictionary<int, DeviceHandle> _devicePool = null;
        public TCPService()
        {
            if (_threadPool == null)
            {
                _threadPool = new List<Thread>();
            }
            if (_messagesQueue == null)
            {
                _messagesQueue = new Queue<Message>();
            }
            if (_tcpListenerThread == null)
            {
                _tcpListenerThread = new Thread(TcpListener);
            }
            if (_messageQueueSem == null)
            {
                _messageQueueSem = new Semaphore(0, 100, "messageQueueSem");
            }
            if (_devicePool == null)
            {
                _devicePool = new Dictionary<int, DeviceHandle>();
            }
            if (!_tcpListenerThread.IsAlive)
            {
                _tcpListenerThread.Start();
            }
        }

        List<Thread> ITcpService.ThreadPool
        {
            get { return _threadPool; }
        }

        Queue<Message> ITcpService.MessageQueue
        {
            get { return _messagesQueue; }
        }

        Dictionary<int, DeviceHandle> ITcpService.DevicePool
        {
            get { return _devicePool; }
        }

        static private void TcpListener()
        {
            IPAddress localAddr = IPAddress.Parse("127.0.0.1");
            TcpListener server = new TcpListener(localAddr, 10808);
            server.Start();
            while (true)
            {
                Console.WriteLine("Waiting for a connection...");
                TcpClient client = server.AcceptTcpClient();
                Thread t = new Thread(TcpClientThread);
                _threadPool.Add(t);
                DeviceHandle deviceHandle = new DeviceHandle(client, t);
                t.Start(client);
                Console.WriteLine("Connected!");
            }
        }

        static private void TcpClientThread(object deviceHandle)
        {
            NetworkStream stream = ((DeviceHandle)deviceHandle).TcpClient.GetStream();
            stream.ReadTimeout = 200;
            byte[] bytes = new byte[256];
            List<byte> data = new List<byte>();

            while (true)
            {
                try
                {
                    int dataLength = stream.Read(bytes, 0, bytes.Length);
                    data.AddRange(bytes);
                }
                catch (System.IO.IOException)
                {
                    if (data.Count != 0)
                    {
                        ReceiveMessage(data, (DeviceHandle)deviceHandle);
                        data.RemoveAll(b => data.Contains(b));
                    }
                }
            }
        }

        static private void ReceiveMessage(List<byte> message, DeviceHandle deviceHandle)
        {
            while ( message.IndexOf(0x7E) < message.IndexOf(0xEF) )
            {
                List<byte> frame = message
                    .Skip(message.FindIndex(b => b == 0x7E))
                    .TakeWhile(b => b != 0xEF)
                    .ToList();
                frame.Add(0xEF);
                if (frame.Count > 2)
                {
                    _messagesQueue.Enqueue(new Message(frame, deviceHandle));
                    frame.ForEach(b => Console.Write("0x{0:X} ", b));
                    Console.WriteLine();
                }
                message.RemoveRange(message.IndexOf(0x7E), frame.Count);
            }
        }

        static private void PerseMessage()
        {
            Message _message;
            while(true)
            {
                _messageQueueSem.WaitOne();
                _message = _messagesQueue.Dequeue();
                if (_message.DeviceHandle.SN == null)
                {

                }
            }
        }
    }
}
