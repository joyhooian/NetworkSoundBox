using System.Collections.Generic;
using System.Threading.Tasks;

namespace NetworkSoundBox.Hubs
{
    public interface INotificationContext
    {
        Dictionary<string, string> ClientDict { get; }

        Task SendClientLogin(string loginKey, string token);
        Task SendDeviceOnline(string openId, string deviceId);
        Task SendDeviceOffline(string openId, string deviceId);
        Task SendDownloadProgress(string openId, float progress);

        void RegisterTempClient(string loginKey, string clientId);
    }
}
