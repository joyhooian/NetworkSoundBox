using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Authorization.DTO
{
    public class UserDto
    {
        public int Id { get; set; }
        public string OpenId { get; set; }
        public string Role { get; set; }
    }
}
