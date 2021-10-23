using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Authorization.Device
{
    public interface IDeviceAuthorization
    {
        bool Authorize(List<byte> requestMessage);
        byte[] GetAuthorization(string sn);
    }
}
