using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Middleware.Authorization.Secret.DTO
{
    public class UserDto
    {
        public int Id { get; set; }
        public string OpenId { get; set; }
        public string Role { get; set; }
    }
}
