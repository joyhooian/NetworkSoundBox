using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Services.Device.Handler
{
    public interface IDeviceContext
    {
        public List<DeviceHandler> DevicePool { get; }
    }
}
