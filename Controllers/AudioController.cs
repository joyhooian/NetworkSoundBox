using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NetworkSoundBox.Entities;
using System.Security.Claims;
using System.Linq;
using NetworkSoundBox.Controllers.Model.Request;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AutoMapper;
using NetworkSoundBox.Models;
using NetworkSoundBox.Controllers.Model.Response;
using System.Collections.Concurrent;
using NetworkSoundBox.Controllers.DTO;
using NetworkSoundBox.Services.Audios;
using NetworkSoundBox.Services.DTO;

namespace NetworkSoundBox.Controllers
{
    [Route("api/audio")]
    [ApiController]
    public class AudioController : ControllerBase
    {
        private readonly MySqlDbContext _dbContext;
        private readonly ILogger<AudioController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;

        private readonly List<string> _acceptAudioTypes;
        private readonly string _audioRootPath;
        private readonly ConcurrentDictionary<string, AudioProcessor> _audioProcessingDict = new();

        private string UserReferenceId { get =>  GetUser(); }

        public AudioController(
            MySqlDbContext dbContext,
            ILogger<AudioController> logger,
            IConfiguration configuration,
            IMapper mapper)
        {
            _configuration = configuration;
            _dbContext = dbContext;
            _logger = logger;
            _mapper = mapper;
            _acceptAudioTypes = _configuration["AcceptAudioTypes"].Split(',').ToList();
            _audioRootPath = _configuration["AudioRootPath"];
        }

        /// <summary>
        /// 上传音频文件到服务器
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("add_audio")]
        public IActionResult AddAudio(IFormFile file)
        {
            if (file == null || file.Length <= 0) 
            {
                return BadRequest("请求为空");
            }

            var userReferenceId = GetUser();
            if (string.IsNullOrEmpty(userReferenceId))
            {
                return Forbid();
            }

            var formFile = file;
            if (_acceptAudioTypes.All(t => !t.Equals(formFile.ContentType, StringComparison.CurrentCulture)))
            {
                return BadRequest($"不支持的格式{formFile.ContentType}");
            }
            if (formFile.Length > int.MaxValue)
            {
                return BadRequest($"文件过大");
            }

            try
            {
                AudioProcessReultToken token = new AudioProcessReultToken();
                _audioProcessingDict.AddOrUpdate(userReferenceId,
                (key) =>
                {
                    var audioProcessor = new AudioProcessor(_dbContext, _audioProcessingDict, userReferenceId);
                    audioProcessor.AddAudioProcess(new AudioProcessDto()
                    {
                        AudioProcessToken = token,
                        FormFile = formFile,
                        RootPath = _audioRootPath,
                    });
                    return audioProcessor;
                },
                (key, value) =>
                {
                    value.AddAudioProcess(new AudioProcessDto()
                    {
                        AudioProcessToken = token,
                        FormFile = formFile,
                        RootPath = _audioRootPath,
                    });
                    return value;
                });
                return token.WaitResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While AddAudio is invoked");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 删除已上传文件
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("delet_audio")]
        public IActionResult DeleteAudio([FromBody] DeleteAudioRequest request)
        {
            if (string.IsNullOrEmpty(request?.AudioReferenceId))
            {
                return BadRequest("请求为空");
            }

            if (string.IsNullOrEmpty(UserReferenceId))
            {
                return Forbid();
            }

            try
            {
                var audioEntity = (from audio in _dbContext.Audios
                                   join cloud in _dbContext.Clouds
                                   on audio.CloudReferenceId equals cloud.CloudReferenceId
                                   where cloud.UserReferenceId == UserReferenceId
                                   where audio.AudioReferenceId == request.AudioReferenceId
                                   where audio.IsCached == "Y"
                                   select audio).FirstOrDefault();
                if (audioEntity == null)
                {
                    return NotFound();
                }

                if (string.IsNullOrEmpty(audioEntity.AudioPath))
                {
                    return NotFound();
                }

                var filePath = $"{_audioRootPath}{audioEntity.AudioPath}";
                System.IO.File.Delete(filePath);

                audioEntity.IsCached = "N";
                _dbContext.Audios.Update(audioEntity);
                _dbContext.SaveChanges();
                return Ok();
            } 
            catch (Exception ex)
            {
                _logger.LogError(ex, "While DeleteAudio is invoked");
                return BadRequest(ex);
            }

        }

        /// <summary>
        /// 获取用户已上传音频文件
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permission")]
        [HttpPost("get_audios")]
        public IActionResult GetAudios()
        {
            var userReferenceId = GetUser();
            if (string.IsNullOrEmpty(userReferenceId))
            {
                return Forbid();
            }

            try
            {
                var cloudEntity = (from cloud in _dbContext.Clouds
                                   where cloud.UserReferenceId == userReferenceId
                                   select cloud).FirstOrDefault();
                if (cloudEntity == null)
                {
                    cloudEntity = new Cloud()
                    {
                        CloudReferenceId = Guid.NewGuid().ToString(),
                        UserReferenceId = userReferenceId,
                        Capacity = 20
                    };
                    _dbContext.Clouds.Add(cloudEntity);
                    _dbContext.SaveChanges();
                    return Ok();
                }

                var audioEntities = (from audio in _dbContext.Audios
                                     join cloud in _dbContext.Clouds
                                     on audio.CloudReferenceId equals cloud.CloudReferenceId
                                     where cloud.UserReferenceId == userReferenceId
                                     select audio).ToList();
                var audios = _mapper.Map<List<Audio>, List<AudioModel>>(audioEntities);
                return Ok(JsonConvert.SerializeObject(new GetAudiosResponse()
                {
                    Audios = audios
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 更新文件名
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize]
        [Authorize(Policy = "Permissioin")]
        [HttpPost("")]
        public IActionResult UpdateAudioName([FromBody] UpdateAudioNameRequest request)
        {
            if (string.IsNullOrEmpty(request?.AudioReferenceId) ||
                string.IsNullOrEmpty(request?.AudioName))
            {
                return BadRequest("请求为空");
            }

            if (string.IsNullOrEmpty(UserReferenceId))
            {
                return Forbid();
            }

            try
            {
                var audioEntity = (from audio in _dbContext.Audios
                                   join cloud in _dbContext.Clouds
                                   on audio.CloudReferenceId equals cloud.CloudReferenceId
                                   where cloud.UserReferenceId == UserReferenceId
                                   where audio.AudioReferenceId == request.AudioReferenceId
                                   select audio).FirstOrDefault();
                if (audioEntity == null)
                {
                    return NotFound();
                }

                var friendlyAudioName = request.AudioName.Replace(' ', '_');
                if ((from audio in _dbContext.Audios
                     join cloud in _dbContext.Clouds
                     on audio.CloudReferenceId equals cloud.CloudReferenceId
                     where cloud.UserReferenceId == UserReferenceId
                     where audio.AudioName == friendlyAudioName
                     select audio).Any())
                {
                    return BadRequest("文件名重复");
                }

                audioEntity.AudioName = friendlyAudioName;
                _dbContext.Audios.Update(audioEntity);
                _dbContext.SaveChanges();
                return Ok(JsonConvert.SerializeObject(new
                {
                    audioEntity.AudioName,
                    audioEntity.Duration,
                    audioEntity.Size
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While UpdateAudioName is invoked");
                return BadRequest(ex.Message);
            }
        }



        private string GetUser()
        {
            return HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
        }

        private void ProcessingUploads()
        {

        }
    }
}
