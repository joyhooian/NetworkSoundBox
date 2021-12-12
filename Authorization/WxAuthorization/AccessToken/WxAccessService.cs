using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using NetworkSoundBox.Authorization.WxAuthorization.AccessToken.DTO;

namespace NetworkSoundBox.Authorization.WxAuthorization.AccessToken
{
    public class WxAccessService : IWxAccessService
    {
        private static WxAccessToken _wxAccessToken;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public WxAccessService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<WxAccessToken> RequestToken()
        {
            if (IsAvaliable())
                return _wxAccessToken;

            string appId = _configuration["WxAccess:AppID"];
            string appSecret = _configuration["WxAccess:AppSecret"];

            var responce = await _httpClientFactory.CreateClient().SendAsync(new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new($"https://api.weixin.qq.com/cgi-bin/token?grant_type=client_credential&appid={appId}&secret={appSecret}")
            });

            string content = await responce.Content.ReadAsStringAsync();

            WxAccessTokenDto dto = JsonConvert.DeserializeObject<WxAccessTokenDto>(content);

            if (dto.ErrorCode == 0)
            {
                _wxAccessToken = new(dto);
                return _wxAccessToken;
            }

            return null;
        }

        private bool IsAvaliable()
        => _wxAccessToken != null && _wxAccessToken.ExpireAt > DateTime.UtcNow;
    }

    public class WxAccessToken
    {
        public string Token { get; private set; }
        public DateTime ExpireAt { get; private set; }

        public WxAccessToken(WxAccessTokenDto dto)
        {
            Token = dto.AccessToken;
            ExpireAt = DateTime.UtcNow.AddSeconds(Convert.ToDouble(dto.Expiration));
        }
    }
}
