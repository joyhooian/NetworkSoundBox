using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Authorization.Jwt.DTO
{
    public class JwtAuthorizationDto
    {
        public long Auths { get; set; }
        public long Expires { get; private set; }
        public bool Success { get; set; } = true;
        public int UserId { get; set; }
        public string OpenId { get; set; }
        public string Token { get; set; }
    }
}
