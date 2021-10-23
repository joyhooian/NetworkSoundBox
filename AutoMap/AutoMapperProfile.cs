using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using NetworkSoundBox.Controllers.DTO;
using NetworkSoundBox.Entities;

namespace NetworkSoundBox.AutoMap
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<Device, DeviceAdminDto>().ReverseMap();
            CreateMap<Device, DeviceCustomerDto>().ReverseMap();

            CreateMap<sbyte, bool>().ConvertUsing(s => s != 0);
        }
    }
}
