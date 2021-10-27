﻿using NetworkSoundBox.Authorization.DTO;
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

namespace NetworkSoundBox.Authorization
{
    public class JwtAppService : IJwtAppService
    {
        private readonly static ISet<JwtAuthorizationDto> _tokens = new HashSet<JwtAuthorizationDto>();
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public JwtAppService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
        }

        public int GetUserId(string token)
        {
            var user = _tokens.FirstOrDefault(jwt => jwt.Token == token);
            if (user != null)
            {
                return user.UserId;
            }
            return 0;
        }

        /// <summary>
        /// 新增Token
        /// </summary>
        /// <param name="userDto">用户信息数据传输对象</param>
        /// <returns></returns>
        public JwtAuthorizationDto Create(UserDto userDto)
        {
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecurityKey"]));

            DateTime authTime = DateTime.UtcNow;
            DateTime expiresAt = authTime.AddMinutes(Convert.ToDouble(_configuration["Jwt:ExpireMinutes"]));

            var identity = new ClaimsIdentity(JwtBearerDefaults.AuthenticationScheme);

            IEnumerable<Claim> claims = new Claim[]
            {
                new Claim(ClaimTypes.Role, userDto.Role),
                new Claim(ClaimTypes.NameIdentifier, userDto.OpenId),
                new Claim(ClaimTypes.Expiration, expiresAt.ToString())
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

            var jwt = new JwtAuthorizationDto
            {
                UserId = userDto.Id,
                Token = tokenHandler.WriteToken(token),
                Auths = new DateTimeOffset(authTime).ToUnixTimeSeconds(),
                Success = true
            };

            _tokens.Add(jwt);

            return jwt;
        }

        /// <summary>
        /// 停用Token
        /// </summary>
        /// <param name="token">Token</param>
        /// <returns></returns>
        public void Deactive(string token)
        => _tokens.Remove(GetExistence(token));

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
        /// <param name="userDto"></param>
        /// <returns></returns>
        public JwtAuthorizationDto Refresh(string token, UserDto userDto)
        {
            var jwtOld = GetExistence(token);
            if (jwtOld == null)
            {
                return new JwtAuthorizationDto()
                {
                    Token = "未获取到当前Token信息",
                    Success = false
                };
            }

            var jwt = Create(userDto);

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

        private JwtAuthorizationDto GetExistence(string token)
        => _tokens.FirstOrDefault(jwt => jwt.Token == token);
    }
}
