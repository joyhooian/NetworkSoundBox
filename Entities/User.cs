using System;
using System.Collections.Generic;

#nullable disable

namespace NetworkSoundBox.Entities
{
    public partial class User
    {
        public int Id { get; set; }
        public string UserRefrenceId { get; set; }
        public string OpenId { get; set; }
        public string Name { get; set; }
        public string AvatarUrl { get; set; }
        public int Role { get; set; }
        public DateTime? UpdateAt { get; set; }
        public DateTime CreateAt { get; set; }
        public int UserTypeKey { get; set; }

        public virtual UserType UserTypeKeyNavigation { get; set; }
    }
}
