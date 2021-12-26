using Microsoft.Extensions.Configuration;
using NetworkSoundBox.Services.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NetworkSoundBox.Authorization.Device
{
    public class DeviceAuthorization : IDeviceAuthorization
    {
        private readonly IConfiguration _configuration;

        private readonly HMACMD5 _hmacmd5Recv;
        private readonly HMACMD5 _hmacmd5Send;

        public DeviceAuthorization(IConfiguration configuration)
        {
            _configuration = configuration;
            _hmacmd5Recv = new HMACMD5(Encoding.ASCII.GetBytes(_configuration["DeviceAuth:SecretKey"]));
            _hmacmd5Send = new HMACMD5(Encoding.ASCII.GetBytes(_configuration["DeviceAuth:ApiKey"]));
        }

        public bool Authorize(List<byte> requestMessage)
        {
            // 初步判断长度是否足够
            if (requestMessage.Count < 9) return false;
            if (!Enum.IsDefined(typeof(DeviceType), (int)requestMessage[^1])) return false;

            DeviceType deviceType = (DeviceType)requestMessage[^1];
            // 如果是4G设备，暂时不鉴权
            if (deviceType == DeviceType.CellularTest) return true;

            // 获取当前时区的整10时间戳
            var timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            timeStamp += (timeStamp % 10 < 5 ? 0 : 10) - timeStamp % 10;
            string timeStampString = "";
            if (deviceType == DeviceType.CellularTest)
            {
                var startTime = new DateTime(1970, 1, 1).ToLocalTime();
                timeStampString = startTime.AddSeconds(timeStamp).ToString("yy/MM/dd, HH:mm:ss");
            }
            else
            {
                timeStampString = timeStamp.ToString();
            }

            // 获取设备SN和登录请求的Token
            var snBytes = requestMessage.GetRange(0, 8).ToArray();
            var tokenBytes = requestMessage.GetRange(8, requestMessage.Count - 9).ToArray();
            var tokenString = Encoding.ASCII.GetString(tokenBytes);

            // 第一次加密
            // 加密对象: SN (ASCII Bytes), Salt: SecretKey (ASCII Bytes)
            // 加密结果: Key Byte数组的十六进制String
            var keyBytes = _hmacmd5Recv.ComputeHash(snBytes);
            var keyString = "";
            foreach(byte b in keyBytes) { keyString += b.ToString("x2"); }

            // 第二次加密
            // 加密对象: 当前地区整十秒timeStamp的字符串，Salt: KeyString (ASCII Bytes)
            using var hmacmd5 = new HMACMD5(Encoding.ASCII.GetBytes(keyString));
            keyBytes = hmacmd5.ComputeHash(Encoding.ASCII.GetBytes(timeStampString));
            keyString = "";
            foreach(byte b in keyBytes) { keyString += b.ToString("x2"); }
            // 比较是否相等
            Console.WriteLine("Our auth_key: {0}, while recv: {1}", keyString, tokenString);
            return keyString == tokenString;
        }

        public byte[] GetAuthorization(string sn)
        {
            var snBytes = Encoding.ASCII.GetBytes(sn);
            // 第一次加密
            // 加密对象: SN (ASCII Bytes), Salt: ApiKey (ASCII Bytes)
            var keyBytes = _hmacmd5Send.ComputeHash(Encoding.ASCII.GetBytes(sn));
            var keyString = "";
            foreach(byte b in keyBytes) { keyString += b.ToString("x2"); }

            // 第二次加密
            // 加密对象: 当前地区整十秒timeStamp的字符串, Salt: KeyString (ASCII Bytes)
            using var hmacmd5 = new HMACMD5(Encoding.ASCII.GetBytes(keyString));
            // 获取时间戳
            var timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            timeStamp += (timeStamp % 10 < 5 ? 0 : 10) - timeStamp % 10;
            var timeStampString = timeStamp.ToString();
            // 开始加密
            keyBytes = hmacmd5.ComputeHash(Encoding.ASCII.GetBytes(timeStampString));
            keyString = "";
            foreach(byte b in keyBytes) { keyString += b.ToString("x2"); }

            return Encoding.ASCII.GetBytes(keyString);
        }
    }
}
