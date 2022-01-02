using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using Newtonsoft.Json;
using NetworkSoundBox.Middleware.Authorization.WxAuthorization.Login.DTO;

namespace NetworkSoundBox.Middleware.Authorization.WxAuthorization.Login
{
    public class WxLoginService : IWxLoginService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        public WxLoginService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<string> Code2Session(string code)
        {
            string appId = _configuration["WxAccess:AppID"];
            string appSecret = _configuration["WxAccess:AppSecret"];

            var responce = await _httpClientFactory.CreateClient().SendAsync(new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"https://api.weixin.qq.com/sns/jscode2session?appid={appId}&secret={appSecret}&js_code={code}&grant_type=authorization_code"),
            });

            string content = await responce.Content.ReadAsStringAsync();
            Code2SessionDto dto = JsonConvert.DeserializeObject<Code2SessionDto>(content);
            return dto.OpenId;
        }
    }
}
