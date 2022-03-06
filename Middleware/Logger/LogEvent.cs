using Microsoft.Extensions.Logging;

namespace NetworkSoundBox.Middleware.Logger
{

    public enum EventEnum
    {
        Default = 0x00,
        ServerHost,

        DeviceConn = 0x10,
        DeviceLogin,
        DeviceDisconn,

        MsgRecv = 0x20,
        MsgSend = 0x21,

        FileProc = 0x30,

        Authorization = 0x40,

        DeviceControlApi = 0x50,
        DeviceMaintainApi,
        DeviceGroupApi,
    }

    public static class LogEvent
    {
        public static EventId Default { get; } = new((int)EventEnum.Default, EventEnum.Default.ToString());
        public static EventId ServerHost { get; } = new((int)EventEnum.ServerHost, EventEnum.ServerHost.ToString());

        public static EventId DeviceConn { get; } = new((int)EventEnum.DeviceConn, EventEnum.DeviceConn.ToString());
        public static EventId DeviceLogin { get; } = new((int)EventEnum.DeviceLogin, EventEnum.DeviceLogin.ToString());
        public static EventId DeviceDisconn { get; } = new((int)EventEnum.DeviceDisconn, EventEnum.DeviceDisconn.ToString());

        public static EventId MsgRecv { get; } = new((int)EventEnum.MsgRecv, EventEnum.MsgRecv.ToString());
        public static EventId MsgSend { get; } = new((int)EventEnum.MsgSend, EventEnum.MsgSend.ToString());

        public static EventId FileProc { get; } = new((int)EventEnum.FileProc, EventEnum.FileProc.ToString());

        public static EventId Authorization { get; } = new((int)EventEnum.Authorization, EventEnum.Authorization.ToString());

        public static EventId DeviceControlApi { get; } = new((int)EventEnum.DeviceControlApi, EventEnum.DeviceControlApi.ToString());
        public static EventId DeviceMaintainApi { get; } = new((int) EventEnum.DeviceMaintainApi, EventEnum.DeviceMaintainApi.ToString());
        public static EventId DeviceGroupApi { get; } = new((int) EventEnum.DeviceGroupApi, EventEnum.DeviceGroupApi.ToString());
    }
}
