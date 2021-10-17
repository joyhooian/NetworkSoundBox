using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using NetworkSoundBox.Services.TextToSpeech.DTO;
using System.Net.WebSockets;
using System.Threading;

namespace NetworkSoundBox.Services.TextToSpeech
{
    public class XunfeiTtsService : IXunfeiTtsService
    {
        private readonly IConfiguration _configuration;

        public XunfeiTtsService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<List<byte>> GetSpeech(string text, string vcn = "xiaoyan", int speed = 50, int volume = 50, int pitch = 50)
        {
            string hostUrl = _configuration["Xunfei:HostUrl"];
            string now = DateTime.Now.ToString("R");
            string host = _configuration["Xunfei:Host"];

            var cancallation = new CancellationTokenSource();
            var recvBuffer = new byte[1024 * 20];
            var audioBuffer = new byte[1024 * 1024];
            int audioBufferOffset = 0;
            ResponceDto responce;

            string wssUrl = $"{hostUrl}?authorization={GetAuthStr(now)}&date={now}&host={host}";
            var webSocketClient = new ClientWebSocket();
            await webSocketClient.ConnectAsync(new Uri(wssUrl), cancallation.Token);
            await webSocketClient.SendAsync(GetFrame(text, vcn, speed, volume, pitch), WebSocketMessageType.Text, true, cancallation.Token);
            do
            {
                await webSocketClient.ReceiveAsync(recvBuffer, cancallation.Token);
                var test = Encoding.UTF8.GetString(recvBuffer);
                responce = JsonConvert.DeserializeObject<ResponceDto>(test);
                if (responce.Code != 0)
                {
                    continue;
                }
                var tempAudio = Convert.FromBase64String(responce.ResponseData.Audio);
                tempAudio.CopyTo(audioBuffer, audioBufferOffset);
                audioBufferOffset += tempAudio.Length;
                Array.Clear(recvBuffer, 0, recvBuffer.Length);
            } while (responce.ResponseData.Status == 1);
            await webSocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "", cancallation.Token);

            return new List<byte>(new ArraySegment<byte>(audioBuffer, 0, audioBufferOffset));
        }

        public string GetAuthStr(string dateTime)
        {
            string apiSecret = _configuration["Xunfei:ApiSecret"];
            string host = _configuration["Xunfei:Host"];
            string uri = _configuration["Xunfei:Uri"];
            string apiKey = _configuration["Xunfei:ApiKey"];

            var secretByte = Encoding.UTF8.GetBytes(apiSecret);
            using var hmac256 = new HMACSHA256(secretByte);

            string signatureRaw = $"host: {host}\ndate: {dateTime}\nGET {uri} HTTP/1.1";
            var signatureRawByte = Encoding.UTF8.GetBytes(signatureRaw);
            var signatureShaByte = hmac256.ComputeHash(signatureRawByte);
            string signature = Convert.ToBase64String(signatureShaByte);

            string authorizationRaw = $"api_key=\"{apiKey}\", algorithm=\"hmac-sha256\", headers=\"host date request-line\", signature=\"{signature}\"";
            string authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes(authorizationRaw));
            return authorization;
        }

        public byte[] GetFrame(string text, string vcn, int speed, int volume, int pitch)
        {
            string textFormated;
            if (vcn == "xiaoyan")
            {
                textFormated = "<break time=\"1000ms\"/>" + text + "<break time=\"1500ms\"/>";
            }
            else
            {
                textFormated = $"[p1000]{text}[p1500]";
            }
            var textFormatedByte = Encoding.UTF8.GetBytes(textFormated);

            var frameDto = new FrameDto
            {
                Common = new CommonDto
                {
                    AppId = _configuration["Xunfei:AppId"]
                },
                Business = new BusinessDto
                {
                    Aue = "lame",
                    Sfl = 1,
                    Vcn = vcn,
                    Ttp = "cssml",
                    Speed = speed,
                    Volume = volume,
                    Pitch = pitch,
                    Tte = "UTF8"
                },
                Data = new DataDto
                {
                    Text = Convert.ToBase64String(textFormatedByte),
                    Status = 2
                }
            };
            var frameStr = JsonConvert.SerializeObject(frameDto);
            return Encoding.UTF8.GetBytes(frameStr);
        }
    }
}
