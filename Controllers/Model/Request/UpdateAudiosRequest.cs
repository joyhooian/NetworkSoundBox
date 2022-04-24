using System.Collections.Generic;

namespace NetworkSoundBox.Controllers.Model.Request
{
    public class UpdateAudiosRequest
    {
        public string PlaylistReferenceId { get; set; }
        public List<string> AudioReferenceIds { get; set; }
    }
}
