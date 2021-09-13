﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NetworkSoundBox.Models;
using Newtonsoft.Json;

namespace NetworkSoundBox.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class SoundboxController : ControllerBase
    {
        private readonly ITcpService _tcpService;
        private readonly MySqlDbContext _dbContext;
        public SoundboxController(ITcpService tcpService, MySqlDbContext dbContext)
        {
            _tcpService = tcpService;
            _dbContext = dbContext;
        }

        [HttpGet("Devices/{id}")]
        public string GetDevices(int id)
        {
            using (_dbContext)
            {
                List<Device> list = _dbContext.Device.Where(device => device.userId == id).ToList();
                list.ForEach(device =>
                {
                    if (_tcpService.DevicePool.Find(d => d.SN == device.sn) != null)
                    {
                        device.isOnline = true;
                    }
                    device.activation = "";
                });
                return JsonConvert.SerializeObject(list);
            }
        }

        [HttpGet("DevicesAdmin")]
        public string GetAllDevicesAdmin()
        {
            using (_dbContext)
            {
                List<Device> list = _dbContext.Device.ToList();
                list.ForEach(device =>
                {
                    if (_tcpService.DevicePool.Find(d => d.SN == device.sn) != null)
                    {
                        device.isOnline = true;
                    }
                });
                return JsonConvert.SerializeObject(list);
            }
        }

        [HttpGet("User/{id}")]
        public string GetUserName(int id)
        {
            using (_dbContext)
            {
                var user = _dbContext.User.Find(id);
                return user.Name;
            }
        }

        [HttpGet("PlayAndPause/SN{sn}Action{action}")]
        public string PlayAndPause(string sn, int action)
        {
            DeviceHandle device = null;
            try
            {
                device = _tcpService.DevicePool.TakeWhile(device => device.SN == sn).First();
            }
            catch (Exception) { }
            if (device == null)
            {
                return "Filed! Device is not connected!";
            }
            if (action == 1)
            {
                device.Client.Client.Send(new byte[] { 0x7E, 0x02, 0x01, 0xEF });
            }
            else if (action == 0)
            {
                device.Client.Client.Send(new byte[] { 0x7E, 0x02, 0x02, 0xEF });
            }
            return "Seccess!";
        }

        [HttpGet("NextAndPrevious/SN{sn}Action{action}")]
        public string NextAndPrevious(string sn, int action)
        {
            DeviceHandle device = null;
            try
            {
                device = _tcpService.DevicePool.TakeWhile(device => device.SN == sn).First();
            }
            catch (Exception) { }
            if (device == null)
            {
                return "Filed! Device is not connected!";
            }
            if (action == 1)
            {
                device.Client.Client.Send(new byte[] { 0x7E, 0x02, 0x03, 0xEF });
            }
            else if (action == 0)
            {
                device.Client.Client.Send(new byte[] { 0x7E, 0x02, 0x04, 0xEF });
            }
            return "Seccess!";
        }

        [HttpGet("Volumn/SN{sn}Action{action}")]
        public string Volumn(string sn, int action)
        {
            DeviceHandle device = null;
            try
            {
                device = _tcpService.DevicePool.TakeWhile(device => device.SN == sn).First();
            }
            catch (Exception) { }
            if (device == null)
            {
                return "Filed! Device is not connected!";
            }
            if (action == 1)
            {
                device.Client.Client.Send(new byte[] { 0x7E, 0x02, 0x05, 0xEF });
            }
            else if (action == 0)
            {
                device.Client.Client.Send(new byte[] { 0x7E, 0x02, 0x06, 0xEF });
            }
            return "Seccess!";
        }

        [HttpGet("StopPlay/SN{sn}")]
        public string StopPlay(string sn)
        {
            DeviceHandle device = null;
            try
            {
                device = _tcpService.DevicePool.TakeWhile(device => device.SN == sn).First();
            }
            catch (Exception) { }
            if (device == null)
            {
                return "Filed! Device is not connected!";
            }
            device.Client.Client.Send(new byte[] { 0x7E, 0x02, 0x0E, 0xEF });
            return "Seccess!";
        }
    }
}
