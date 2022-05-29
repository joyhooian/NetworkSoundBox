using System;

namespace NetworkSoundBox.Controllers.Model.Request
{
    public class AddCronTaskRequest
    {
        public string Weekdays { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public int Volume { get; set; }
        public int Relay { get; set; }
        public string AudioReferenceId { get; set; }
    }
}
