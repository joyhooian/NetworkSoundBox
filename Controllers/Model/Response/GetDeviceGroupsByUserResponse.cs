namespace NetworkSoundBox.Controllers.Model.Response
{
    public class GetDeviceGroupsByUserResponse
    {
        public string DeviceGroupReferenceId { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
        public string CreateTime { get; set; }
        public string UpdateTime { get; set; }
    }
}
