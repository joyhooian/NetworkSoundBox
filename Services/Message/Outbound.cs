using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.Message
{
    public class Outbound : Message
    {
        public MessageToken Token { get; }

        public Outbound(Command command, params byte[] param)
        {
            Command = command;
            Token = null;
            Data ??= new List<byte>();
            
            Data.Add(StartByte);
            Data.Add((byte)Command);
            Data.Add((byte)(param.Length >> 8));
            Data.Add((byte)param.Length);
            Data.AddRange(param);
            Data.Add(EndByte);
            MessageLen = Data.Count;
        }

        public Outbound(Command command, MessageToken token = null, params byte[] param)
        {
            Command = command;
            Token = token;
            Data ??= new List<byte>();

            Data.Add(StartByte);
            Data.Add((byte)Command);
            Data.Add((byte)(param.Length >> 8));
            Data.Add((byte)param.Length);
            Data.AddRange(param);
            Data.Add(EndByte);
            MessageLen = Data.Count;
        }

        public Outbound(Command command, int packageIndex, MessageToken token = null, params byte[] param)
        {
            Command = command;
            Token = token;
            Data ??= new List<byte>();

            Data.Add(StartByte);
            Data.Add((byte)Command);
            Data.Add((byte)(packageIndex >> 8));
            Data.Add((byte)packageIndex);
            Data.AddRange(param);
            Data.Add(EndByte);
            MessageLen = Data.Count;
        }
    }
}
