using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NetworkSoundBox.Entities;
using System.Security.Claims;
using System.Linq;
using System;
using Newtonsoft.Json;
using AutoMapper;
using NetworkSoundBox.Models;
using System.Collections.Generic;
using NetworkSoundBox.Controllers.Model.Request;

namespace NetworkSoundBox.Controllers
{
    [Route("api/playlist")]
    [ApiController]
    public class PlaylistController : ControllerBase
    {
        private readonly MySqlDbContext _dbcontext;
        private readonly ILogger<PlaylistController> _logger;
        private readonly IMapper _mapper;
        private string UserReferenceId { get => GetUser(); }

        public PlaylistController(
            MySqlDbContext dbContext,
            ILogger<PlaylistController> logger,
            IMapper mapper)
        {
            _mapper = mapper;
            _dbcontext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// 添加播放列表
        /// </summary>
        /// <param name="name">播放列表名</param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("add")]
        public IActionResult AddPlaylist([FromQuery] string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return BadRequest("请求为空");
            }

            if (string.IsNullOrEmpty(UserReferenceId))
            {
                return Forbid();
            }

            try
            {
                var playlistEntity = (from playlist in _dbcontext.Playlists
                                      where playlist.UserReferenceId == UserReferenceId
                                      where playlist.PlaylistName == name
                                      select playlist).FirstOrDefault();
                if (playlistEntity != null)
                {
                    return BadRequest("播放列表名称重复");
                }

                playlistEntity = new Playlist()
                {
                    PlaylistReferenceId = Guid.NewGuid().ToString(),
                    UserReferenceId = UserReferenceId,
                    PlaylistName = name
                };

                _dbcontext.Playlists.Add(playlistEntity);
                _dbcontext.SaveChanges();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While AddPlaylist is invoked");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 添加音频到播放列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("add_audio")]
        public IActionResult AddAudios([FromBody] PlaylistAddAudiosRequest request)
        {
            if (string.IsNullOrEmpty(request?.PlaylistReferenceId) || request.AudioReferenceIds.Count == 0) return BadRequest("请求为空");

            if (string.IsNullOrEmpty(UserReferenceId)) return Forbid();

            var audioEntities = new List<Audio>();

            try
            {
                foreach (var audioReferenceId in request.AudioReferenceIds)
                {
                    if (string.IsNullOrEmpty(audioReferenceId)) continue;
                    var audioEntity = (from audio in _dbcontext.Audios
                                       join cloud in _dbcontext.Clouds
                                       on audio.CloudReferenceId equals cloud.CloudReferenceId
                                       where cloud.UserReferenceId == UserReferenceId
                                       where audio.AudioReferenceId == audioReferenceId
                                       select audio).FirstOrDefault();
                    if (audioEntities != null) audioEntities.Add(audioEntity);
                }

                foreach (var audioEntity in audioEntities)
                {
                    var playlistAudioEntity = (from playlistAudio in _dbcontext.PlaylistAudios
                                               join playlist in _dbcontext.Playlists
                                               on playlistAudio.PlaylistReferenceId equals playlist.PlaylistReferenceId
                                               where playlist.PlaylistReferenceId == request.PlaylistReferenceId
                                               where playlist.UserReferenceId == UserReferenceId
                                               where playlistAudio.AudioReferenceId == audioEntity.AudioReferenceId
                                               select playlistAudio).FirstOrDefault();
                    if (playlistAudioEntity != null) continue;

                    playlistAudioEntity = new PlaylistAudio()
                    {
                        PlaylistReferenceId = request.PlaylistReferenceId,
                        AudioReferenceId = audioEntity.AudioReferenceId
                    };
                    _dbcontext.PlaylistAudios.Add(playlistAudioEntity);
                }
                _dbcontext.SaveChanges();

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While AddAudios is invoked");
                return BadRequest(ex.Message);
            }
        }

        [Authorize(Policy = "Permission")]
        [HttpPost("delete_playlist")]
        public IActionResult DeletePlaylist([FromQuery] string playlistReferenceId)
        {
            if (string.IsNullOrEmpty(playlistReferenceId)) return BadRequest("请求为空");
            if (string.IsNullOrEmpty(UserReferenceId)) return Forbid();
            try
            {
                var playlistEntity = (from playlist in _dbcontext.Playlists
                                      where playlist.UserReferenceId == UserReferenceId
                                      where playlist.PlaylistReferenceId == playlistReferenceId
                                      select playlist).FirstOrDefault();
                if (playlistEntity == null) return Ok();

                var playlistAudioEntities = (from pla in _dbcontext.PlaylistAudios
                                             where pla.PlaylistReferenceId == playlistReferenceId
                                             select pla).ToList();
                var deviceEntities = (from device in _dbcontext.Devices
                                      where device.PlaylistReferenceId == playlistReferenceId
                                      select device).ToList();
                foreach(var deviceEntity in deviceEntities)
                {
                    deviceEntity.PlaylistReferenceId = null;
                }
                var deviceGroupEntities = (from deviceGroup in _dbcontext.DeviceGroups
                                           where deviceGroup.PlaylistReferenceId == playlistReferenceId
                                           select deviceGroup).ToList();
                foreach(var deviceGroupEntity in deviceGroupEntities)
                {
                    deviceGroupEntity.PlaylistReferenceId = null;
                }
                _dbcontext.Playlists.Remove(playlistEntity);
                _dbcontext.PlaylistAudios.RemoveRange(playlistAudioEntities);
                _dbcontext.Devices.UpdateRange(deviceEntities);
                _dbcontext.DeviceGroups.UpdateRange(deviceGroupEntities);
                _dbcontext.SaveChanges();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While DeletePlaylist is invoked");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 获取所有播放列表
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("get_playlist")]
        public IActionResult GetPlaylist()
        {
            if (string.IsNullOrEmpty(UserReferenceId))
            {
                return Forbid();
            }

            try
            {
                var playlistEntities = (from playlist in _dbcontext.Playlists
                                        where playlist.UserReferenceId == UserReferenceId
                                        select playlist).ToList();

                return Ok(JsonConvert.SerializeObject(playlistEntities));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Where GetPlaylist is invoked");
                return BadRequest(ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 获取播放列表所有音频
        /// </summary>
        /// <param name="playlistReferenceId"></param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("get_playlist_content")]
        public IActionResult GetPlaylistContent([FromQuery] string playlistReferenceId)
        {
            if (string.IsNullOrEmpty(playlistReferenceId)) return BadRequest("请求为空");

            if (string.IsNullOrEmpty(UserReferenceId)) return Forbid();

            try
            {
                var audioEntities = (from playlistAudio in _dbcontext.PlaylistAudios
                                             join playlist in _dbcontext.Playlists
                                             on playlistAudio.PlaylistReferenceId equals playlist.PlaylistReferenceId
                                             join audio in _dbcontext.Audios
                                             on playlistAudio.AudioReferenceId equals audio.AudioReferenceId
                                             where playlist.UserReferenceId == UserReferenceId
                                             where playlistAudio.PlaylistReferenceId == playlistReferenceId
                                             select audio).ToList();
                return Ok(JsonConvert.SerializeObject(audioEntities));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While GetPlaylistContent is invoked");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 获取所有使用某播放列表的设备
        /// </summary>
        /// <param name="playlistReferenceId"></param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("get_devices")]
        public IActionResult GetDevices([FromQuery] string playlistReferenceId)
        {
            if (string.IsNullOrEmpty(playlistReferenceId)) return BadRequest("请求为空");

            if (string.IsNullOrEmpty(UserReferenceId)) return Forbid();

            try
            {
                var deviceEntities = (from device in _dbcontext.Devices
                                      where device.PlaylistReferenceId == playlistReferenceId
                                      select device).ToList();
                var deviceModels = _mapper.Map<List<Device>, List<DeviceModel>>(deviceEntities);

                return Ok(JsonConvert.SerializeObject(deviceModels));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While GetDevices is invoked");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 获取所有使用某播放列表的设备组
        /// </summary>
        /// <param name="playlistReferenceId"></param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("get_device_group")]
        public IActionResult GetDeviceGroups([FromQuery] string playlistReferenceId)
        {
            if (string.IsNullOrEmpty(playlistReferenceId)) return BadRequest("请求为空");

            if (string.IsNullOrEmpty(UserReferenceId)) return Forbid();

            try
            {
                var deviceGroupEntities = (from deviceGroup in _dbcontext.DeviceGroups
                                           where deviceGroup.PlaylistReferenceId == playlistReferenceId
                                           select deviceGroup).ToList();

                return Ok(JsonConvert.SerializeObject(deviceGroupEntities));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While GetDeviceGroups is invoked");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 更新Playlist名称
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("update_playlist")]
        public IActionResult UpdatePlaylist([FromBody] UpdatePlaylistRequest request)
        {
            if (string.IsNullOrEmpty(request?.PlaylistReferenceId) || string.IsNullOrEmpty(request.PlaylistName)) return BadRequest("请求为空");

            if (string.IsNullOrEmpty(UserReferenceId)) return Forbid();

            try
            {
                var playlistEntity = (from playlist in _dbcontext.Playlists
                                      where playlist.UserReferenceId == UserReferenceId
                                      where playlist.PlaylistReferenceId == request.PlaylistReferenceId
                                      select playlist).FirstOrDefault();
                if (playlistEntity == null) return NotFound();

                if (_dbcontext.Playlists
                    .Where(p =>
                        p.UserReferenceId == UserReferenceId &&
                        p.PlaylistName == request.PlaylistName &&
                        p.PlaylistReferenceId != request.PlaylistReferenceId)
                     .Any())
                {
                    return BadRequest("播放列表名称重复");
                }

                playlistEntity.PlaylistName = request.PlaylistName;
                _dbcontext.Playlists.Update(playlistEntity);
                _dbcontext.SaveChanges();

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While UpdatePlaylist is invoked");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 更新Playlist音频
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize(Policy = "Permission")]
        [HttpPost("update_audios")]
        public IActionResult UpdateAudios([FromBody] UpdateAudiosRequest request)
        {
            if (string.IsNullOrEmpty(request?.PlaylistReferenceId)) return BadRequest("请求为空");

            if (string.IsNullOrEmpty(UserReferenceId)) return Forbid();

            try
            {
                var playlistEntity = (from playlist in _dbcontext.Playlists
                                      where playlist.UserReferenceId == UserReferenceId
                                      where playlist.PlaylistReferenceId == request.PlaylistReferenceId
                                      select playlist).FirstOrDefault();
                if (playlistEntity == null) return NotFound();

                var audioEntities = new List<Audio>();
                foreach (var audioReferenceId in request.AudioReferenceIds)
                {
                    var audioEntity = (from audio in _dbcontext.Audios
                                       join cloud in _dbcontext.Clouds
                                       on audio.CloudReferenceId equals cloud.CloudReferenceId
                                       where cloud.UserReferenceId == UserReferenceId
                                       where audio.AudioReferenceId == audioReferenceId
                                       select audio).FirstOrDefault();
                    if (audioEntities != null) audioEntities.Add(audioEntity);
                }

                var previousPlaylistAudioEntities = (from playlistAudio in _dbcontext.PlaylistAudios
                                                     join playlist in _dbcontext.Playlists
                                                     on playlistAudio.PlaylistReferenceId equals playlist.PlaylistReferenceId
                                                     where playlist.UserReferenceId == UserReferenceId
                                                     where playlistAudio.PlaylistReferenceId == request.PlaylistReferenceId
                                                     select playlistAudio).ToList();

                var newPlaylistAudioEntities = new List<PlaylistAudio>();
                foreach (var audioEntity in audioEntities)
                {
                    var playlistAudioEntity = new PlaylistAudio()
                    {
                        PlaylistReferenceId = request.PlaylistReferenceId,
                        AudioReferenceId = audioEntity.AudioReferenceId,
                    };
                    newPlaylistAudioEntities.Add(playlistAudioEntity);
                }

                _dbcontext.PlaylistAudios.RemoveRange(previousPlaylistAudioEntities);
                _dbcontext.PlaylistAudios.AddRange(newPlaylistAudioEntities);
                _dbcontext.SaveChanges();
                return Ok(JsonConvert.SerializeObject(newPlaylistAudioEntities));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While UpdateAudios is invoked");
                return BadRequest(ex.Message);
            }
        }

        private string GetUser()
        {
            return HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
        }
    }
}
