using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class User
    {
        public User()
        {
            Devices = new HashSet<Device>();
        }

        public uint Id { get; set; }
        public string Name { get; set; }
        public string TelNum { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Email { get; set; }
        public string Openid { get; set; }
        public string Role { get; set; }

        public virtual ICollection<Device> Devices { get; set; }
    }
}
