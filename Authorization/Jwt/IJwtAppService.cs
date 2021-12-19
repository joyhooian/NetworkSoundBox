using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NetworkSoundBox.Authorization.Jwt.DTO;
using NetworkSoundBox.Authorization.Secret.DTO;

namespace NetworkSoundBox.Authorization.Jwt
{
    public interface IJwtAppService
    {
        JwtAuthorizationDto Create(UserDto userDto);
        JwtAuthorizationDto Refresh(string token, UserDto userDto);
        int GetUserId(string token);
        string GetOpenId(string token);
        bool IsCurrentActiveToken();
        void DeactiveCurrent();
        bool IsActive(string token);
        void Deactive(string token);
    }
}
