using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Controllers.DTO
{
    public class LoginResultDto
    {
        public UserInfoDto UserInfo { get; set; }
        public string Status { get; set; }
        public string Token { get; set; }
        public string ErrorMessage { get; set; }
    }
}
