using System.Collections.Generic;
using System.Threading.Tasks;

namespace NetworkSoundBox.Middleware.Hubs
{
    public interface INotificationContext
    {
        Dictionary<string, string> ClientDict { get; }

        Task SendClientLogin(string loginKey, string token);
        Task SendDeviceOnline(string sn);
        Task SendDeviceOffline(string sn);
        Task SendDownloadProgress(float progress, string sn);

        void RegisterTempClient(string loginKey, string clientId);
    }
}
