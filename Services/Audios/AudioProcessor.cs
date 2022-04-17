using Microsoft.AspNetCore.Http;
using NetworkSoundBox.Entities;
using NetworkSoundBox.Services.DTO;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace NetworkSoundBox.Services.Audios
{
    public class AudioProcessor
    {
        private readonly ConcurrentDictionary<string, AudioProcessor> _audioDict;
        private readonly BlockingCollection<AudioProcessDto> _audioCloudDtoQueue = new();
        private readonly MySqlDbContext _dbContext;
        private Task _task;
        private string _userReferenceId;

        public AudioProcessor(
            MySqlDbContext dbContext, 
            ConcurrentDictionary<string, AudioProcessor> audioDict,
            string userReferenceId)
        {
            _dbContext = dbContext;
            _audioDict = audioDict;
            _userReferenceId = userReferenceId;
        }

        public AudioProcessReultToken AddAudioProcess(AudioProcessDto dto)
        {
            dto.AudioProcessToken = new AudioProcessReultToken();
            _audioCloudDtoQueue.Add(dto);
            if (_task == null) Processing();
            return dto.AudioProcessToken;
        }

        private void Processing()
        {
            _task = Task.Run(async () =>
            {
                Console.WriteLine($"Audio Processing Task Running at {DateTime.Now.ToLocalTime()}");
                while (_audioCloudDtoQueue.TryTake(out var audioProcessDto))
                {
                    var token = audioProcessDto.AudioProcessToken;
                    var formFile = audioProcessDto.FormFile;

                    try
                    {
                        var audioEntity = (from audio in _dbContext.Audios
                                           join cloud in _dbContext.Clouds
                                           on audio.CloudReferenceId equals cloud.CloudReferenceId
                                           where cloud.UserReferenceId == _userReferenceId
                                           where audio.AudioName == formFile.FileName
                                           select audio).FirstOrDefault();
                        if (audioEntity != null)
                        {
                            token.Success = false;
                            token.ErrorMessage = "文件名重复";
                            token.Semaphore.Release();
                        }

                        var cloudEntity = (from cloud in _dbContext.Clouds
                                           where cloud.UserReferenceId == _userReferenceId
                                           select cloud).FirstOrDefault();
                        if (cloudEntity == null)
                        {
                            cloudEntity = new Cloud
                            {
                                UserReferenceId = _userReferenceId,
                                CloudReferenceId = Guid.NewGuid().ToString(),
                                Capacity = 20
                            };
                            _dbContext.Clouds.Add(cloudEntity);
                            _dbContext.SaveChanges();
                        }
                        else
                        {
                            if (!CheckCapacity(_userReferenceId))
                            {
                                token.Success = false;
                                token.ErrorMessage = "容量已满, 请删除不用的文件或扩容";
                                token.Semaphore.Release();
                            }
                        }

                        var content = new byte[formFile.Length];
                        await formFile.OpenReadStream().ReadAsync(content);

                        var friendlyFileName = formFile.FileName.Replace(' ', '_');
                        var path = $"{audioProcessDto.RootPath}/{_userReferenceId.Replace('-', '_')}";
                        if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);
                        var fullPath = $"{path}/{friendlyFileName}";
                        using (var fileStream = System.IO.File.Create(fullPath))
                        {
                            fileStream.Write(content);
                            fileStream.Close();
                        }

                        audioEntity = new Audio
                        {
                            AudioReferenceId = Guid.NewGuid().ToString(),
                            CloudReferenceId = cloudEntity.CloudReferenceId,
                            AudioPath = $"/{_userReferenceId}/{friendlyFileName}",
                            AudioName = friendlyFileName,
                            Size = Convert.ToInt32(formFile.Length),
                            IsCached = "Y"
                        };
                        _dbContext.Audios.Add(audioEntity);
                        _dbContext.SaveChanges();
                        token.Success = true;
                        token.ResponseMesssage = JsonConvert.SerializeObject(new
                        {
                            audioEntity.AudioName,
                            audioEntity.Duration,
                            audioEntity.Size,
                        });
                        token.Semaphore.Release();
                    }
                    catch (Exception ex)
                    {
                        token.Success = false;
                        token.ErrorMessage = ex.Message;
                        token.Semaphore.Release();
                    }
                }
                _audioDict.TryRemove(new KeyValuePair<string, AudioProcessor>(_userReferenceId, this));
            });
        }
        private bool CheckCapacity(string userReferenceId)
        {
            var usedCount = (from audio in _dbContext.Audios
                             join cloud in _dbContext.Clouds
                             on audio.CloudReferenceId equals cloud.CloudReferenceId
                             where cloud.UserReferenceId == userReferenceId
                             where audio.IsCached == "Y"
                             select audio).Count();
            var capacity = (from cloud in _dbContext.Clouds
                            where cloud.UserReferenceId == userReferenceId
                            select cloud.Capacity).FirstOrDefault();
            return usedCount < capacity;
        }
    }
}
