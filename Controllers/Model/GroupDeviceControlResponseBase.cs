using NetworkSoundBox.Entities;
using System.Collections.Generic;

namespace NetworkSoundBox.Controllers.Model
{
    public class GroupDeviceControlResponseBase
    {
        public List<SuccessDevice> SuccessDevices { get; set; }
        public List<FailureDevice> FailureDevices { get; set; }
    }

    public class FailureDevice
    {
        
        public string DeviceRefereceId { get; set; }
        public string Reason { get; set; }

        public enum FailureType
        {
            PermissionDenied,
            DeviceNoResponed
        }
        public FailureDevice(Device device, FailureType type)
        {
            DeviceRefereceId = device.DeviceReferenceId;
            Reason = type switch
            {
                FailureType.PermissionDenied => "没有操作权限",
                FailureType.DeviceNoResponed => "设备未响应",
                _ => string.Empty
            };
        }

        public FailureDevice(string deviceReferenceId, string reason)
        {
            DeviceRefereceId = deviceReferenceId;
            Reason = reason;
        }
    }

    public class SuccessDevice
    {
        public string DeviceReferenceId { get; set; }
        public string Value { get; set; }

        public SuccessDevice(Device device, object value)
        {
            DeviceReferenceId = device.DeviceReferenceId;
            Value = value?.ToString()??string.Empty;
        }
    }
}
