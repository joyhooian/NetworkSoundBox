using NetworkSoundBox.Services.Device.Handler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.Message
{
    public class Inbound : Message
    {
        public Inbound(List<byte> message)
        {
            Command = (Command)message[1];
            MessageLen = message[2] | message[3];
            Data = message.Skip(4).Take(MessageLen).ToList();
        }

        public static void ParseMessage(List<byte> data, DeviceHandler device)
        {
            while (true)
            {
                int startOffset = data.IndexOf(START_BYTE);
                if (startOffset < 0)
                {
                    break;
                }
                //!!长度检查
                int endOffset = FindEnd(startOffset, data);
                if (data[endOffset] == END_BYTE && Enum.IsDefined(typeof(Command), (int)data[startOffset + 1]))
                {
                    try
                    {
                        device.InboxQueue.Add(new Inbound(data.Skip(startOffset).Take(endOffset - startOffset + 1).ToList()));
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

    }
}
