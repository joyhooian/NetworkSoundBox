using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.Message
{
    public class Outbound : Message
    {
        public Token Token { get; }

        public Outbound(Command command, Token token = null, params byte[] param)
        {
            Command = command;
            Token = token ?? new(null, null, null);
            if (Data == null)
                Data = new();

            Data.Add(START_BYTE);
            Data.Add((byte)Command);
            Data.Add((byte)(param.Length >> 8));
            Data.Add((byte)param.Length);
            Data.AddRange(param);
            Data.Add(END_BYTE);
            MessageLen = Data.Count;
        }

        public Outbound(Command command, int packageIndex, Token token = null, params byte[] param)
        {
            Command = command;
            Token = token ?? new(null, null, null);
            if (Data == null)
                Data = new();

            Data.Add(START_BYTE);
            Data.Add((byte)Command);
            Data.Add((byte)(packageIndex >> 8));
            Data.Add((byte)packageIndex);
            Data.AddRange(param);
            Data.Add(END_BYTE);
            MessageLen = Data.Count;
        }
    }
}
