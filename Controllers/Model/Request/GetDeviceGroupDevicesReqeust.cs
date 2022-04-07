namespace NetworkSoundBox.Controllers.Model.Request
{
    public class GetDeviceGroupDevicesReqeust
    {
        public string DeviceGroupReferenceId { get; set; }
        public bool? IsIncludeOtherDevice { get; set; }
    }
}
