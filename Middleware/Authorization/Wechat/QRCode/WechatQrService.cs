using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc;
using NetworkSoundBox.Middleware.Authorization.Wechat.QRCode.Model;
using NetworkSoundBox.Middleware.Authorization.Wechat.AccessToken;
using NetworkSoundBox.Middleware.Authorization.Wechat.AccessToken.Model;
using Microsoft.Extensions.Logging;
using NetworkSoundBox.Middleware.Logger;

namespace NetworkSoundBox.Middleware.Authorization.Wechat.QRCode
{
    public class WechatQrService : IWechatQrService
    {
        private readonly IWechatAccessService _wechatAccessService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WechatQrService> _logger;

        public WechatQrService(
            ILogger<WechatQrService> logger,
            IWechatAccessService wechatAccessService, 
            IHttpClientFactory httpClientFactory)
        {
            _wechatAccessService = wechatAccessService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<WechatQrLoginData> GetWechatLoginQrAsync()
        {
            try
            {
                var wechatToken = await _wechatAccessService.GetWechatAccessToken();
                if (wechatToken == null)
                {
                    _logger.LogError(LogEvent.Authorization, "Got null instance while trying to retrieve wechat access token");
                    return null;
                }

                string uri = $"https://api.weixin.qq.com/wxa/getwxacodeunlimit?access_token={wechatToken.Token}";
                string loginKey = Guid.NewGuid().ToString("N");
                string json = JsonConvert.SerializeObject(new WechatQrRequest
                {
                    Scene = loginKey,
                    Page = "pages/myself/login"
                });
                var responce = await _httpClientFactory.CreateClient().PostAsync(uri, new StringContent(json));
                responce.EnsureSuccessStatusCode();

                if (responce.Content.Headers.ContentType.MediaType == "image/jpeg")
                {
                    return new WechatQrLoginData
                    {
                        LoginKey = loginKey,
                        QRCode = await responce.Content.ReadAsByteArrayAsync()
                    };
                }
                else
                {
                    _logger.LogError(LogEvent.Authorization, "The content type mismatch while trying to retrieve QR code");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(LogEvent.Authorization, ex, "Error occured while trying to retrieve QR code");
                return null;
            }
        }
    }
}
