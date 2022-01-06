using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using NetworkSoundBox.Middleware.Authorization.Wechat.AccessToken.Model;
using Microsoft.Extensions.Logging;
using NetworkSoundBox.Middleware.Logger;

namespace NetworkSoundBox.Middleware.Authorization.Wechat.AccessToken
{
    public class WechatAccessService : IWechatAccessService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WechatAccessService> _logger;

        private static WechatAccessToken _wechatAccessTokenCache;


        public WechatAccessService(
            ILogger<WechatAccessService> logger,
            IHttpClientFactory httpClientFactory, 
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<WechatAccessToken> GetWechatAccessToken()
        {
            if (_wechatAccessTokenCache == null || _wechatAccessTokenCache.ExpireAt < DateTimeOffset.UtcNow.AddMinutes(1f))
            {
                if (await GetWechatAccessTokenAsync() != null)
                {
                    return _wechatAccessTokenCache;
                }
                return null;
            }
            return _wechatAccessTokenCache;
        }

        private async Task<GetWechatAccessTokenResponse> GetWechatAccessTokenAsync()
        {
            string appId = _configuration["WxAccess:AppID"];
            string appSecret = _configuration["WxAccess:AppSecret"];

            try
            {
                var responce = await _httpClientFactory.CreateClient().SendAsync(new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new($"https://api.weixin.qq.com/cgi-bin/token?grant_type=client_credential&appid={appId}&secret={appSecret}")
                });
                responce.EnsureSuccessStatusCode();
                var getWechatAccessTokenResponse = JsonConvert.DeserializeObject<GetWechatAccessTokenResponse>(await responce.Content.ReadAsStringAsync());
                if (getWechatAccessTokenResponse.ErrorCode == 0)
                {
                    _wechatAccessTokenCache = new WechatAccessToken()
                    {
                        Token = getWechatAccessTokenResponse.AccessToken,
                        ExpireAt = DateTimeOffset.UtcNow.AddSeconds(Convert.ToDouble(getWechatAccessTokenResponse.Expiration))
                    };
                    return getWechatAccessTokenResponse;
                }
                else
                {
                    _logger.LogError(LogEvent.Authorization, getWechatAccessTokenResponse.ErrorMessage);
                    return null;
                }
            }
            catch(Exception e)
            {
                _logger.LogError(LogEvent.Authorization, e, "Error occured when trying to get wechat access token");
                return null;
            }
        }
    }
}
