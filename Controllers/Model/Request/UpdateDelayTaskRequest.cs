namespace NetworkSoundBox.Controllers.Model.Request
{
    public class UpdateDelayTaskRequest
    {
        public string DelayReferenceId { get; set; }
        public int DelayTime { get; set; }
        public int Volume { get; set; }
        public int Relay { get; set; }
        public string AudioReferenceId { get; set; }
    }
}
