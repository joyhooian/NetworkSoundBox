using Nsb.Type;

namespace NetworkSoundBox.Models
{
    public class UserModel
    {
        public int Id { get; set; }
        public string UserRefrenceId { get; set; }
        public string OpenId { get; set; }
        public string Name { get; set; }
        public string AvatarUrl { get; set; }
        public RoleType Role { get; set; }
    }
}
