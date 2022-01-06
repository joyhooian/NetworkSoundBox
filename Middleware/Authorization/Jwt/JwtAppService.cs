using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System.Text;
using System.Security.Claims;
using NetworkSoundBox.Models;
using NetworkSoundBox.Middleware.Authorization.Jwt.Model;

namespace NetworkSoundBox.Middleware.Authorization.Jwt
{
    public class JwtAppService : IJwtAppService
    {
        private static readonly Dictionary<string, JwtModel> _tokensDict = new();
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public JwtAppService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
        }

        public int GetUserId(string token)
        {
            if (string.IsNullOrEmpty(token)) return 0;
            var jwt = GetExistence(token);
            return jwt?.UserId ?? 0;
        }

        public string GetOpenId(string token)
        {
            if (string.IsNullOrEmpty (token)) return string.Empty;
            var jwt = GetExistence(token);
            return jwt?.Token ?? string.Empty;
        }

        /// <summary>
        /// 新增Token
        /// </summary>
        /// <param name="userDto">用户信息数据传输对象</param>
        /// <returns></returns>
        public JwtModel Create(UserModel userModel)
        {
            #region 生成token
            JwtSecurityTokenHandler tokenHandler = new();
            SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_configuration["Jwt:SecurityKey"]));

            DateTime authTime = DateTime.UtcNow;
            DateTime expiresAt = authTime.AddMinutes(Convert.ToDouble(_configuration["Jwt:ExpireMinutes"]));

            var identity = new ClaimsIdentity(JwtBearerDefaults.AuthenticationScheme);

            IEnumerable<Claim> claims = new Claim[]
            {
                new Claim(ClaimTypes.Role, userModel.Role.ToString().ToLower()),
                new Claim(ClaimTypes.NameIdentifier, userModel.OpenId),
                new Claim(ClaimTypes.Expiration, expiresAt.ToString()),
            };
            identity.AddClaims(claims);

            _httpContextAccessor.HttpContext.SignInAsync(JwtBearerDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                Expires = expiresAt,
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            #endregion
            var jwt = new JwtModel
            {
                UserId = userModel.Id,
                Token = tokenHandler.WriteToken(token),
                Success = true,
                OpenId = userModel.OpenId,
                ExpireAt = expiresAt
            };
            _tokensDict.Add(jwt.Token, jwt);
            return jwt;
        }

        /// <summary>
        /// 停用Token
        /// </summary>
        /// <param name="token">Token</param>
        /// <returns></returns>
        public void Deactive(string token)
        => _tokensDict.Remove(token);

        /// <summary>
        /// 停用当前Token
        /// </summary>
        public void DeactiveCurrent()
        => Deactive(GetCurrent());

        /// <summary>
        /// 判断Token是否有效
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public bool IsActive(string token)
        => GetExistence(token) != null;

        /// <summary>
        /// 判断当前Token是否有效
        /// </summary>
        /// <returns></returns>
        public bool IsCurrentActiveToken()
        => IsActive(GetCurrent());

        /// <summary>
        /// 刷新 Token
        /// </summary>
        /// <param name="token"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public JwtModel Refresh(string token, UserModel user)
        {
            var jwtOld = GetExistence(token);
            if (jwtOld == null)
            {
                return new JwtModel()
                {
                    Token = string.Empty,
                    Success = false
                };
            }

            var jwt = Create(user);

            DeactiveCurrent();

            return jwt;
        }

        private string GetCurrent()
        {
            var authorizationHeader = _httpContextAccessor.HttpContext.Request.Headers["authorization"];

            return authorizationHeader == StringValues.Empty
                ? string.Empty
                : authorizationHeader.Single().Split(" ").Last();
        }

        private JwtModel GetExistence(string token)
        {
            if (_tokensDict.TryGetValue(token, out var jwt))
            {
                return jwt;
            }
            return null;
        }
    }
}
