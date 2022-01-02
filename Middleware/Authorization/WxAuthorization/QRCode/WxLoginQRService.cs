using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc;
using NetworkSoundBox.Middleware.Authorization.WxAuthorization.AccessToken;
using NetworkSoundBox.Middleware.Authorization.WxAuthorization.QRCode.DTO;

namespace NetworkSoundBox.Middleware.Authorization.WxAuthorization.QRCode
{
    public class WxLoginQRService : IWxLoginQRService
    {
        private static HashSet<string> _loginKeyHashSet = new HashSet<string>();
        private readonly IWxAccessService _wxAccessService;
        private readonly IHttpClientFactory _httpClientFactory;

        public HashSet<string> LoginKeyHashSet { get => _loginKeyHashSet; }

        public WxLoginQRService(IWxAccessService wxAccessService, IHttpClientFactory httpClientFactory)
        {
            _wxAccessService = wxAccessService;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<WxLoginQRDto> RequestLoginQRAsync()
        {
            WxAccessToken token = await _wxAccessService.RequestToken();
            if (token == null)
            {
                return new WxLoginQRDto
                {
                    Success = false
                };
            }

            string uri = $"https://api.weixin.qq.com/wxa/getwxacodeunlimit?access_token={token.Token}";
            string loginKey = Guid.NewGuid().ToString("N");
            string json = JsonConvert.SerializeObject(new WxRequestQRDto
            {
                Scene = loginKey
            });

            var responce = await _httpClientFactory.CreateClient().PostAsync(uri, new StringContent(json));

            if (responce.Content.Headers.ContentType.MediaType == "image/jpeg")
            {
                byte[] qrImage;
                qrImage = await responce.Content.ReadAsByteArrayAsync();
                return new WxLoginQRDto
                {
                    LoginKey = loginKey,
                    QRCode = new FileContentResult(qrImage, "image/jpeg"),
                    Success = true
                };
            }

            return null;
        }
    }
}
