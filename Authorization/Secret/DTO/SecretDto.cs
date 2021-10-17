using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkSoundBox.Authorization.DTO
{
    public class SecretDto
    {
        public string Code { get; set; }
        public Guid LoginKey { get; set; }
    }
}
