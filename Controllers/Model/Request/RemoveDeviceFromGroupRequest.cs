namespace NetworkSoundBox.Controllers.Model.Request
{
    public class RemoveDeviceFromGroupRequest
    {
        public string DeviceGroupReferenceId { get; set; }
        public string DeviceReferenceId { get; set; }
    }
}