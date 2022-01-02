using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using NetworkSoundBox.Controllers.DTO;
using NetworkSoundBox.Entities;
using NetworkSoundBox.Middleware.Authorization.Secret.DTO;

namespace NetworkSoundBox.Middleware.AutoMap
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<Device, DeviceAdminDto>().ReverseMap();
            CreateMap<Device, DeviceCustomerDto>().ReverseMap();
            CreateMap<User, UserDto>().ReverseMap();
            CreateMap<User, UserInfoDto>().ReverseMap();

            CreateMap<sbyte, bool>().ConvertUsing(s => s != 0);
        }
    }
}
