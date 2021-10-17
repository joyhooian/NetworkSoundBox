using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NetworkSoundBox.Authorization.DTO;

namespace NetworkSoundBox.Authorization
{
    public interface IJwtAppService
    {
        JwtAuthorizationDto Create(UserDto userDto);
        JwtAuthorizationDto Refresh(string token, UserDto userDto);
        bool IsCurrentActiveToken();
        void DeactiveCurrent();
        bool IsActive(string token);
        void Deactive(string token);
    }
}
