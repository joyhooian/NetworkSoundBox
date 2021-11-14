using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.Device.Handler
{
    public class DeviceContext : IDeviceContext
    {
        public static readonly List<DeviceHandler> DevicePool = new List<DeviceHandler>();

        List<DeviceHandler> IDeviceContext.DevicePool => DevicePool;
    }
}
