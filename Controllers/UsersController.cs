using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetworkSoundBox.Controllers.Model;
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
        /// 用户扫描登陆二维码后, 通过微信认证用户身份, 通过SignalR通知页面jwt
        /// </summary>
        /// <param name="code"></param>
        /// <param name="loginKey"></param>
        /// <returns></returns>
        [HttpPost("wx_auth")]
        public async Task<IActionResult> WxAuth(string code, string loginKey)
        {
            string openId = await _wxLoginService.GetWechatOpenId(code);
            if (string.IsNullOrEmpty(openId)) return BadRequest("微信端服务无法访问, 请稍后再试");
            var userEntity = _dbContext.Users.FirstOrDefault(u => u.OpenId == openId);
            if (userEntity == null) return BadRequest("用户未注册");
            var userModel = _mapper.Map<User, UserModel>(userEntity);
            var jwt = _jwtAppService.Create(userModel);
            if (_notificationContext.ClientDict.ContainsKey(loginKey))
            {
                await _notificationContext.SendClientLogin(loginKey, jwt.Token);
                return Ok();
            }
            return BadRequest("请重试");
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
            if (User == null)
            {
                userEntity = new User()
                {
                    Name = wxLoginRequest.NickName,
                    OpenId = openId,
                    AvatarUrl = wxLoginRequest.AvatarUrl,
                    Role = (int)RoleType.Customer
                };
                _dbContext.Users.Add(userEntity);
                _dbContext.SaveChanges();
            }
            _dbContext.Entry(userEntity);

            var userModel = _mapper.Map<User, UserModel>(userEntity);
            var jwt = _jwtAppService.Create(userModel);

            var wxLoginResponse = _mapper.Map<UserModel, WxLoginResponse>(userModel);
            wxLoginResponse.Token = jwt.Token;
            return Ok(JsonConvert.SerializeObject(wxLoginResponse));
        }

        /// <summary>
        /// 网页用户获取用户信息
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [HttpPost("get_uinfo")]
        public IActionResult GetUserInfo()
        {
            var openId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            var userEntity = _dbContext.Users.FirstOrDefault(user => user.OpenId == openId);
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
