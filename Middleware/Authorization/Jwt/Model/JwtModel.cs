using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Middleware.Authorization.Jwt.Model
{
    public class JwtModel
    {
        public bool Success { get; set; } = true;
        public int UserId { get; set; }
        public string UserRefrenceId { get; set; }
        public string Token { get; set; }
        public DateTimeOffset ExpireAt { get; set; }
    }
}
