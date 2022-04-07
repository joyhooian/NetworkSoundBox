namespace NetworkSoundBox.Controllers.Model.Request
{
    public class GroupDeviceControlRequestBase
    {
        public string DeviceGroupReferenceId { get; set; }
        public int Action { get; set; }
    }
}