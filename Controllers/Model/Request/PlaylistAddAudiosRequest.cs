using System.Collections.Generic;

namespace NetworkSoundBox.Controllers.Model.Request
{
    public class PlaylistAddAudiosRequest
    {
        public string PlaylistReferenceId { get; set; }
        public List<string> AudioReferenceIds { get; set; }
    }
}
