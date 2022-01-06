using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;

namespace NetworkSoundBox.Services.Message
{
    public enum Command
    {
        None = 0xFF,

        // Communication
        Activation = 0x00,
        Login = 0x01,
        Heartbeat = 0x02,

        // Device Control
        Reboot = 0x10,
        FactoryReset = 0x11,

        // Timing
        LoopWhile = 0x20,
        QueryTimingMode = 0x21,
        QueryTimingSet = 0x22,
        SetTimingAlarm = 0x23,
        SetTimingAfter = 0x24,
        TimingReport = 0x25,

        // File Progress
        FileTransReqWifi = 0xA0,
        FileTransProcWifi = 0xA1,
        FileTransErrWifi = 0xA2,
        FileTransRptWifi = 0xA3,
        FileTransReqCell = 0xA4,
        FileTransRptCell = 0xA5,

        // Play Control
        Play = 0xF0,
        Pause = 0xF1,
        Next = 0xF2,
        Previous = 0xF3,
        Volume = 0xF4,
        FastForward = 0xF5,
        FastBackward = 0xF6,
        PlayIndex = 0xF7,
        ReadFilesList = 0xF8,
        DeleteFile = 0xF9
    }

    public enum MessageStatus
    {
        Untouched,
        Sending,
        Sent,
        Replied,
        Canceled,
        Failed
    }

    public class Message
    {
        protected const byte StartByte = 0x7E;
        protected const byte EndByte = 0xEF;
        public Command Command { get; protected init; }
        public int MessageLen { get; protected init; }
        public List<byte> Data { get; protected init; }
    }

    public class MessageToken
    {
        private MessageStatus _status;
        private readonly ManualResetEventSlim _processDone;
        private readonly byte[] _expRplData;
        public byte[] RepliedData { get; set; }
        public Command ExpRplCmd { get; }
        public bool IsValidate { get; private set; }

        public MessageStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                if (_status >= MessageStatus.Replied)
                    _processDone.Set();
            }
        }


        public MessageToken(Command expRplCmd, byte[] expRplData = null)
        {
            _processDone = new ManualResetEventSlim();
            ExpRplCmd = expRplCmd;
            _expRplData = expRplData;
        }

        public MessageStatus Wait()
        {
            _processDone.Wait();
            return _status;
        }

        public bool SetData(List<byte> data)
        {
            if (data.IsNullOrEmpty()) return true;
            RepliedData = new byte[data.Count];
            data.CopyTo(RepliedData);
            IsValidate = CheckReply();
            return IsValidate;
        }

        public void SetFailed()
        {
            Status = MessageStatus.Failed;
        }

        public void SetCanceled()
        {
            Status = MessageStatus.Canceled;
        }

        public void SetReplied()
        {
            Status = MessageStatus.Replied;
        }

        public void SetSent()
        {
            Status = MessageStatus.Sent;
        }

        public void SetSending()
            => Status = MessageStatus.Sending;

        private bool CheckReply()
        {
            if (_expRplData == null) return true;
            if (_expRplData.Length != RepliedData.Length) return false;
            for (var index = 0; index < _expRplData.Length; index++)
            {
                if (_expRplData[index] != RepliedData[index]) return false;
            }

            return true;
        }
    }
}