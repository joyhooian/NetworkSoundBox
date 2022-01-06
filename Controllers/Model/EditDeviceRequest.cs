﻿using System;

namespace NetworkSoundBox.Controllers.Model
{
    public class EditDeviceRequest
    {
        public string Sn { get; set; }
        public string DeviceType { get; set; }
        public bool Activation { get; set; }
        public string ActivationKey { get; set; }
        public string Name { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastOnline { get; set; }
    }
}
