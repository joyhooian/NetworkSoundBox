using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NetworkSoundBox.Middleware.Authorization.Jwt.Model;
using NetworkSoundBox.Models;

namespace NetworkSoundBox.Middleware.Authorization.Jwt
{
    public interface IJwtAppService
    {
        JwtModel Create(UserModel user);
        JwtModel Refresh(string token, UserModel user);
        int GetUserId(string token);
        string GetOpenId(string token);
        bool IsCurrentActiveToken();
        void DeactiveCurrent();
        bool IsActive(string token);
        void Deactive(string token);
    }
}
