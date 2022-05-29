namespace NetworkSoundBox.Controllers.Model.Request
{
    public class AddTTSRequest
    {
        public string Text { get; set; }
        public string VCN { get; set; }
        public int Speed { get; set; }
        public int Volume { get; set; }
        public string Name { get; set; }
    }
}
