using NetworkSoundBox.WxAuthorization.QRCode.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.WxAuthorization.QRCode
{
    public interface IWxLoginQRService
    {
        Task<WxLoginQRDto> RequestLoginQRAsync();
        HashSet<string> LoginKeyHashSet { get; }
    }
}
