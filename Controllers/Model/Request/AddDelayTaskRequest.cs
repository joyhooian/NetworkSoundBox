namespace NetworkSoundBox.Controllers.Model.Request
{
    public class AddDelayTaskRequest
    {
        public int DelayTime { get; set; }
        public int Volume { get; set; }
        public int Relay { get; set; }
        public string AudioReferenceId { get; set; }
    }
}
