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
        QUERY_TIMING_MODE = 0x21,
        QUERY_TIMING_SET = 0x22,
        SET_TIMING_ALARM = 0x23,
        SET_TIMING_AFTER = 0x24,
        TIMING_REPORT = 0x25,

        // File Progress
        FILE_TRANS_REQ_WIFI = 0xA0,
        FILE_TRANS_PROC_WIFI = 0xA1,
        FILE_TRANS_ERR_WIFI = 0xA2,
        FILE_TRANS_RPT_WIFI = 0xA3,
        FILE_TRANS_REQ_CELL = 0xA4,
        FILE_TRANS_RPT_CELL = 0xA5,

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
