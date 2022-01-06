using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetworkSoundBox.Middleware.Authorization.Wechat.Login.Model;
using NetworkSoundBox.Middleware.Logger;
using Newtonsoft.Json;

namespace NetworkSoundBox.Middleware.Authorization.Wechat.Login
{
    public class WechatLoginService : IWechatLoginService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WechatLoginService> _logger;

        public WechatLoginService(
            ILogger<WechatLoginService> logger,
            IConfiguration configuration, 
            IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string> GetWechatOpenId(string code)
        {
            string appId = _configuration["WxAccess:AppID"];
            string appSecret = _configuration["WxAccess:AppSecret"];

            try
            {
                var responce = await _httpClientFactory.CreateClient().SendAsync(new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"https://api.weixin.qq.com/sns/jscode2session?appid={appId}&secret={appSecret}&js_code={code}&grant_type=authorization_code"),
                });
                responce.EnsureSuccessStatusCode();
                string content = await responce.Content.ReadAsStringAsync();
                WechatLoginResponse wechatLoginResponse = JsonConvert.DeserializeObject<WechatLoginResponse>(content);
                if (wechatLoginResponse.ErrorCode == 0)
                {
                    return wechatLoginResponse.OpenId;
                }
                else
                {
                    _logger.LogError(LogEvent.Authorization, wechatLoginResponse.ErrorMessage);
                    return null;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(LogEvent.Authorization, e, "");
                return null;
            }

        }
    }
}
