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
    public class MyDevice
    {
        public static List<MyDevice> DevicePool = new List<MyDevice>();
        public Socket Socket { get; set; }
        public List<byte> Data { get; }
        public byte[] receiveBuffer { get; }
        public CancellationTokenSource CancellationTokenSource { get; }

        public MyDevice(Socket socket)
        {
            Socket = socket;
            Data = new List<byte>();
            receiveBuffer = new byte[100];
            CancellationTokenSource = new CancellationTokenSource();
            var task = ReceiveTask(CancellationTokenSource.Token);
        }

        protected async Task ReceiveTask(CancellationToken cancellationToken)
        {
            await Task.Yield();
            Socket.ReceiveTimeout = 200;
            int receiveCount = 0;
            while(true)
            {
                try
                {
                    receiveCount =  Socket.Receive(receiveBuffer);
                    if (receiveCount == 0)
                    {
                        break;
                    }
                    else
                    {
                        Data.Clear();
                        for(int cnt = 0; cnt < receiveCount; cnt++)
                        {
                            Data.Add(receiveBuffer[cnt]);
                        }
                    }
                }
                catch(Exception)
                {
                    if (receiveCount != 0)
                    {
                        Socket.Send(receiveBuffer, receiveCount, 0);
                        receiveCount = 0;
                    }
                }
            }
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
                MyDevice.DevicePool.Add(new MyDevice(server.AcceptSocket()));
            }
        }
    }
}
