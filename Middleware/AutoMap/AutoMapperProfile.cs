using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using NetworkSoundBox.Controllers.DTO;
using NetworkSoundBox.Controllers.Model.Request;
using NetworkSoundBox.Controllers.Model.Response;
using NetworkSoundBox.Entities;
using NetworkSoundBox.Models;
using Nsb.Type;

namespace NetworkSoundBox.Middleware.AutoMap
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<Device, DeviceAdminDto>().ReverseMap();
            CreateMap<Device, GetDevicesCustomerResponse>()
                .ForMember(
                    dest => dest.DeviceType,
                    opt => opt.MapFrom(s => ((Nsb.Type.DeviceType)s.Type).ToString().ToLower()));
            CreateMap<User, UserInfoDto>().ReverseMap();

            CreateMap<sbyte, bool>().ConvertUsing(s => s != 0);
            #region New Model-Entity Mapping
            CreateMap<UserModel, User>().ReverseMap();
            #endregion

            #region Controller Model Mapping
            CreateMap<UserModel, WxLoginResponse>().ReverseMap();
            CreateMap<User, GetUserInfoResponse>()
                .ForMember(
                    dest => dest.Role,
                    opt => opt.MapFrom(s => ((RoleType)s.Role).ToString().ToLower())
                )
                .ReverseMap();
            object type;
            CreateMap<EditDeviceAdminRequest, Device>()
                .ForMember(
                    dest => dest.Type,
                    opt => opt.MapFrom(s => Enum.TryParse(typeof(Nsb.Type.DeviceType), s.DeviceType, true, out type) ? (int)type : 0))
                .ReverseMap();
            CreateMap<Device, GetDevicesAdminResponse>()
                .ForMember(dest => dest.DeviceType, opt => opt.MapFrom(s => ((Nsb.Type.DeviceType)s.Type).ToString().ToLower()))
                .ForMember(dest => dest.Activation, opt => opt.MapFrom(s => s.IsActived.Equals(1)));
            CreateMap<AddDeviceRequest, Device>().ForMember(dest => dest.Type, opt => opt.MapFrom(s => Enum.Parse(typeof(Nsb.Type.DeviceType), s.DeviceType, true)));
            CreateMap<Device, DeviceModel>()
                .ForMember(dest => dest.Type, opt => opt.MapFrom(s => ((Nsb.Type.DeviceType)s.Type).ToString().ToLower()));
            #endregion
        }
    }
}
