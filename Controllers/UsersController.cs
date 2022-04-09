using System;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetworkSoundBox.Controllers.Model.Request;
using NetworkSoundBox.Controllers.Model.Response;
using NetworkSoundBox.Entities;
using NetworkSoundBox.Middleware.Authorization.Jwt;
using NetworkSoundBox.Middleware.Authorization.Wechat.Login;
using NetworkSoundBox.Middleware.Authorization.Wechat.QRCode;
using NetworkSoundBox.Middleware.Authorization.Wechat.QRCode.Model;
using NetworkSoundBox.Middleware.Hubs;
using NetworkSoundBox.Models;
using Newtonsoft.Json;
using Nsb.Type;

namespace NetworkSoundBox.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IMapper _mapper;
        private readonly IJwtAppService _jwtAppService;
        private readonly MySqlDbContext _dbContext;
        private readonly IWechatQrService _wxLoginQRService;
        private readonly IWechatLoginService _wxLoginService;
        private readonly INotificationContext _notificationContext;

        public UsersController(
            IMapper mapper,
            INotificationContext notificationContext,
            IWechatLoginService wxLoginService,
            IWechatQrService wxLoginQRService,
            IJwtAppService jwtAppService, 
            MySqlDbContext dbContext)
        {
            _jwtAppService = jwtAppService;
            _dbContext = dbContext;
            _wxLoginQRService = wxLoginQRService;
            _wxLoginService = wxLoginService;
            _notificationContext = notificationContext;
            _mapper = mapper;
        }

        /// <summary>
        /// 生成微信登陆二维码
        /// </summary>
        /// <returns></returns>
        [HttpPost("wx_web_login")]
        public async Task<string> WxLogin()
        {
            WechatQrLoginData dto = await _wxLoginQRService.GetWechatLoginQrAsync();
            return JsonConvert.SerializeObject(dto);
        }

        /// <summary>
        /// 调试登录
        /// </summary>
        /// <param name="loginKey"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        [HttpPost("dev_login")]
        public async Task<IActionResult> DevLogin(string loginKey, string role)
        {
            if (string.IsNullOrEmpty(role) || string.IsNullOrEmpty(loginKey))
            {
                return BadRequest();
            }

            var userRole = 0;
            if (role.Contains("admin"))
            {
                userRole = 1;
            }
            else if (role.Contains("customer"))
            {
                userRole= 2;
            }
            else
            {
                return BadRequest();
            }

            var userEntity = (from user in _dbContext.Users
                              where user.Role == userRole &&
                              user.OpenId == "abcdefg"
                              select user)
                              ?.FirstOrDefault();
            if (userEntity == null)
            {
                return NotFound();
            }
            var userModel = _mapper.Map<User, UserModel>(userEntity);
            var jwt = _jwtAppService.Create(userModel, LoginType.Web);
            await _notificationContext.SendClientLogin(loginKey, jwt.Token);
            return Ok();
        }

        /// <summary>
        /// 检查登录态
        /// </summary>
        /// <returns></returns>
        [HttpPost("check_login")]
        public IActionResult CheckLogin()
        {
            return Ok(_jwtAppService.IsCurrentActiveToken());
        }

        /// <summary>
        /// 更新用户资料
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("update_profile")]
        public IActionResult UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            if (request == null) return BadRequest();

            var userReferenceId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            if (string.IsNullOrEmpty(userReferenceId)) return Forbid();

            var userEntity = _dbContext.Users.FirstOrDefault(u => u.UserRefrenceId == userReferenceId);
            if (userEntity == null) return BadRequest("找不到用户");

            if (!string.IsNullOrEmpty(request.Name)) userEntity.Name = request.Name;
            if (!string.IsNullOrEmpty(request.AvatarUrl) && 
                Regex.IsMatch(request.AvatarUrl, @"^http(s)?://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?$"))
            {
                userEntity.AvatarUrl = request.AvatarUrl;
            }
            _dbContext.Users.Update(userEntity);
            _dbContext.SaveChanges();

            return Ok();
        }

        /// <summary>
        /// 用户扫描登陆二维码后, 通过微信认证用户身份, 通过SignalR通知页面jwt
        /// </summary>
        /// <param name="code"></param>
        /// <param name="loginKey"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("wx_auth")]
        public async Task<IActionResult> WxAuth(string loginKey)
        {
            var userRefrenceId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            var userEntity = (from user in _dbContext.Users
                             where user.UserRefrenceId == userRefrenceId
                             select user).First();
            var userModel = _mapper.Map<User, UserModel>(userEntity);
            var jwt = _jwtAppService.Create(userModel, LoginType.Web);
            if (_notificationContext.ClientDict.ContainsKey(loginKey))
            {
                await _notificationContext.SendClientLogin(loginKey, jwt.Token);
                return Ok("成功");
            }
            return BadRequest("请重试");
        }

        /// <summary>
        /// 用户登出
        /// </summary>
        /// <returns></returns>
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            _jwtAppService.DeactiveCurrent();
            return Ok();
        }

        /// <summary>
        /// 微信用户登陆使用
        /// </summary>
        /// <param name="wxLoginRequest"></param>
        /// <returns></returns>
        [HttpPost("wx_login")]
        public async Task<IActionResult> WxLogin([FromBody] WxLoginRequest wxLoginRequest)
        {
            string openId = await _wxLoginService.GetWechatOpenId(wxLoginRequest.Code);
            if (string.IsNullOrEmpty(openId)) return BadRequest("登陆失败");

            var userEntity = _dbContext.Users.FirstOrDefault(u => u.OpenId == openId);
            if (userEntity == null)
            {
                userEntity = new Entities.User()
                {
                    Name = wxLoginRequest.NickName,
                    OpenId = openId,
                    UserRefrenceId = Guid.NewGuid().ToString(),
                    AvatarUrl = wxLoginRequest.AvatarUrl,
                    Role = (int)RoleType.Customer,
                };
                _dbContext.Users.Add(userEntity);
                _dbContext.SaveChanges();
            }
            _dbContext.Entry(userEntity);

            var userModel = _mapper.Map<User, UserModel>(userEntity);
            var jwt = _jwtAppService.Create(userModel, LoginType.WeApp);

            var wxLoginResponse = _mapper.Map<UserModel, WxLoginResponse>(userModel);
            wxLoginResponse.Token = jwt.Token;
            return Ok(JsonConvert.SerializeObject(wxLoginResponse));
        }

        /// <summary>
        /// 微信登录
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        [HttpPost("wechat_login")]
        public async Task<IActionResult> WechatLogin([FromQuery] string code)
        {
            string openId = await _wxLoginService.GetWechatOpenId(code);
            if (string.IsNullOrEmpty(openId)) return BadRequest("登陆失败");

            var userEntity = _dbContext.Users.FirstOrDefault(u => u.OpenId == openId);
            if (userEntity == null)
            {
                userEntity = new User()
                {
                    Name = string.Empty,
                    OpenId = openId,
                    AvatarUrl = string.Empty,
                    UserRefrenceId = Guid.NewGuid().ToString(),
                    Role = (int)RoleType.Customer
                };
                _dbContext.Users.Add(userEntity);
                _dbContext.SaveChanges();
            }

            var userModel = _mapper.Map<User, UserModel>(userEntity);
            var jwt = _jwtAppService.Create(userModel, LoginType.WeApp);

            return Ok(JsonConvert.SerializeObject(
                new
                {
                    userEntity.Name,
                    userEntity.AvatarUrl,
                    jwt.Token
                }));
        }

        /// <summary>
        /// 网页用户获取用户信息
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("get_uinfo")]
        public IActionResult GetUserInfo()
        {
            var userRefrenceId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            var userEntity = _dbContext.Users.FirstOrDefault(user => user.UserRefrenceId == userRefrenceId);
            if (userEntity == null) return BadRequest("未知错误");
            var getUserInfoResp = _mapper.Map<User, GetUserInfoResponse>(userEntity);
            return Ok(JsonConvert.SerializeObject(getUserInfoResp));
        }

        //[HttpGet("userInfo/{token}")]
        //public string GetUserInfo(string token)
        //{
        //    int uid = _jwtAppService.GetUserId(token);
        //    var user = _dbContext.Users.FirstOrDefault(user => user.Id == uid);
        //    if (user != null)
        //    {
        //        var dto =  new WebUserInfoDto
        //        {
        //            Roles = user.Role.Split(',').ToList(),
        //            Introduction = "Nothing to say",
        //            Avatar = "https://wpimg.wallstcn.com/f778738c-e4f8-4870-b634-56703b4acafe.gif",
        //            Name = "Super Admin"
        //        };
        //        return JsonConvert.SerializeObject(dto);
        //    }
        //    return "Error! No such user!";
        //}
    }
}
