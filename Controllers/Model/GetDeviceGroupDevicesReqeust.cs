namespace NetworkSoundBox.Controllers.Model
{
    public class GetDeviceGroupDevicesReqeust
    {
        public string DeviceGroupReferenceId { get; set; }
        public bool ? IsIncludeOtherDevice { get; set; }
    }
}
