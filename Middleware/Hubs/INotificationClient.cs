using System.Threading.Tasks;

namespace NetworkSoundBox.Middleware.Hubs
{
    public interface INotificationClient
    {
        Task DeviceOnline(string deviceInfo);
        Task DeviceOffline(string deviceInfo);
        Task DownloadProgress(string progress);
        Task AudioSyncComplete(string sn, string audioReferenceId);
        Task AudioSyncFail(string deviceReferenceId, string audioReferenceId);
        Task Login(string token);
    }
}
