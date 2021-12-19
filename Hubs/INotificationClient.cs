using System.Threading.Tasks;

namespace NetworkSoundBox.Hubs
{
    public interface INotificationClient
    {
        Task DeviceOnline(string deviceInfo);
        Task DeviceOffline(string deviceInfo);
        Task DownloadProgress(string progress);
        Task Login(string token);
    }
}
