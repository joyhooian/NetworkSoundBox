namespace NetworkSoundBox.Controllers.Model.Response
{
    public class GetPlayListGroupResponse : GroupDeviceControlResponseBase
    {
        public int MinAudioCount { get; set; } = int.MaxValue;
    }
}
