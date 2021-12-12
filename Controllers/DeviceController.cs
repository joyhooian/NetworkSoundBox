using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetworkSoundBox.Services.Device.Handler;
using System;
using System.Collections.Generic;
using System.Linq;
using NetworkSoundBox.Entities;
using System.Security.Claims;
using NetworkSoundBox.Filter;

namespace NetworkSoundBox.Controllers
{
    [Route("api/device")]
    [ApiController]
    public class DeviceController : ControllerBase
    {
        private readonly IDeviceContext _deviceContext;
        private readonly MySqlDbContext _dbContext;

        public DeviceController(
            MySqlDbContext dbContext,
            IDeviceContext deviceContext)
        {
            _deviceContext = deviceContext;
            _dbContext = dbContext;
        }

        /// <summary>
        /// 获取播放列表
        /// </summary>
        /// <param name="sn">SN</param>
        /// <returns></returns>
        [Authorize]
        [ServiceFilter(typeof(ResourceAuthAttribute))]
        [HttpPost("play_list")]
        public IActionResult GetPlayList([FromQuery] string sn)
        {
            return Ok();
        }

        /// <summary>
        /// 删除指定音频
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="index">音频序号</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("delete_audio")]
        public string DeleteAudio([FromQuery] string sn, int index)
        {
            DeviceHandler device = _deviceContext.DevicePool.FirstOrDefault(device => device.SN == sn);

            if (device == null) return "Failed! Device is not connected!";

            if (device.DeleteAudio(index)) return "Success!";
            return "Failed!";
        }
    }
}
