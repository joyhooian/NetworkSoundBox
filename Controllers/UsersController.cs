using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NetworkSoundBox.Controllers.DTO;
using Newtonsoft.Json;
using NetworkSoundBox.Entities;
using NetworkSoundBox.Authorization.Jwt;

namespace NetworkSoundBox.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IJwtAppService _jwtAppService;
        private readonly MySqlDbContext _dbContext;

        public UsersController(IJwtAppService jwtAppService, MySqlDbContext dbContext)
        {
            _jwtAppService = jwtAppService;
            _dbContext = dbContext;
        }

        [HttpGet("userInfo/{token}")]
        public string GetUserInfo(string token)
        {
            int uid = _jwtAppService.GetUserId(token);
            var user = _dbContext.Users.FirstOrDefault(user => user.Id == uid);
            if (user != null)
            {
                var dto =  new WebUserInfoDto
                {
                    Roles = user.Role.Split(',').ToList(),
                    Introduction = "Nothing to say",
                    Avatar = "https://wpimg.wallstcn.com/f778738c-e4f8-4870-b634-56703b4acafe.gif",
                    Name = "Super Admin"
                };
                return JsonConvert.SerializeObject(dto);
            }
            return "Error! No such user!";
        }
    }
}
