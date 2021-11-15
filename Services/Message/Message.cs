using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.Message
{
    public enum Command
    {
        NONE = 0xFF,

        // Communication
        ACTIVATION = 0x00,
        LOGIN = 0x01,
        HEARTBEAT = 0x02,

        // Device Control
        REBOOT = 0x10,
        FACTORY_RESET = 0x11,

        // Timing
        LOOP_WHILE = 0x20,

        // File Progress
        FILE_TRANS_REQ = 0xA0,
        FILE_TRANS_PROC = 0xA1,
        FILE_TRANS_ERR = 0xA2,
        FILE_TRANS_RPT = 0xA3,

        // Play Control
        PLAY = 0xF0,
        PAUSE = 0xF1,
        NEXT = 0xF2,
        PREVIOUS = 0xF3,
        VOLUMN = 0xF4,
        FAST_FORWARD = 0xF5,
        FAST_BACKWARD = 0xF6,
        PLAY_INDEX = 0xF7,
        READ_FILES_LIST = 0xF8,
        DELETE_FILE = 0xF9
    }

    public enum DeviceType
    {
        WiFi_Test = 0x01,
        Cellular_Test = 0x11
    }
    
    public enum MessageStatus
    {
        Untouched,
        Sending,
        Sent,
        Replied,
        Failed
    }
    
    public class Message
    {
        protected const byte START_BYTE = 0x7E;
        protected const byte END_BYTE = 0xEF;
        public Command Command { get; protected set; }
        public int MessageLen { get; protected set; }
        public List<byte> Data { get; protected set; }
    }
}
