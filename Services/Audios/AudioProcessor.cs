using Microsoft.AspNetCore.Http;
using NetworkSoundBox.Entities;
using NetworkSoundBox.Services.DTO;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace NetworkSoundBox.Services.Audios
{
    public class AudioProcessor
    {
        private readonly ConcurrentDictionary<string, AudioProcessor> _audioDict;
        private readonly BlockingCollection<AudioProcessDto> _audioCloudDtoQueue = new();
        private Task _task;
        private string _userReferenceId;
        public string HashId { get; set; } = Guid.NewGuid().ToString();

        public AudioProcessor(
            ConcurrentDictionary<string, AudioProcessor> audioDict,
            string userReferenceId)
        {
            _audioDict = audioDict;
            _userReferenceId = userReferenceId;
        }

        public AudioProcessReultToken AddAudioProcess(AudioProcessDto dto)
        {
            _audioCloudDtoQueue.Add(dto);
            lock(this)
            {
                if (_task == null || _task.IsCompleted)
                {
                    Processing();
                }
            }
            return dto.AudioProcessToken;
        }

        private void Processing()
        {
            _task = Task.Run(async() =>
            {
                Console.WriteLine($"Audio Processing Task(ID: {_task.Id}) Running at {DateTime.Now.ToLocalTime()}");
                Console.WriteLine($"Queue depth: {_audioCloudDtoQueue.Count} at the beginning");
                while (_audioCloudDtoQueue.TryTake(out var audioProcessDto))
                {
                    var token = audioProcessDto.AudioProcessToken;
                    var formFile = audioProcessDto.FormFile;

                    try
                    {
                        await using var db = new MySqlDbContext(new DbContextOptionsBuilder<MySqlDbContext>().Options);
                        var audioEntity = (from audio in db.Audios
                                           join cloud in db.Clouds
                                           on audio.CloudReferenceId equals cloud.CloudReferenceId
                                           where cloud.UserReferenceId == _userReferenceId
                                           where audio.AudioName == formFile.FileName
                                           select audio).FirstOrDefault();
                        if (audioEntity != null)
                        {
                            token.Success = false;
                            token.ErrorMessage = "文件名重复";
                            token.Semaphore.Release();
                            continue;
                        }

                        var cloudEntity = (from cloud in db.Clouds
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
                            db.Clouds.Add(cloudEntity);
                            await db.SaveChangesAsync();
                        }
                        else
                        {
                            if (!CheckCapacity(_userReferenceId))
                            {
                                token.Success = false;
                                token.ErrorMessage = "容量已满, 请删除不用的文件或扩容";
                                token.Semaphore.Release();
                                continue;
                            }
                        }

                        var content = new byte[formFile.Length];
                        await formFile.OpenReadStream().ReadAsync(content);

                        var friendlyFileName = formFile.FileName.Replace(' ', '_');
                        var path = $"{audioProcessDto.RootPath}/{_userReferenceId}";
                        if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);
                        var fullPath = $"{path}/{friendlyFileName}";
                        await using (var fileStream = System.IO.File.Create(fullPath))
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
                        db.Audios.Add(audioEntity);
                        await db.SaveChangesAsync();
                        token.Success = true;
                        token.ResponseMesssage = JsonConvert.SerializeObject(new
                        {
                            audioEntity.AudioName,
                            audioEntity.Duration,
                            audioEntity.Size,
                        });
                        token.Semaphore.Release();
                        continue;
                    }
                    catch (Exception ex)
                    {
                        token.Success = false;
                        token.ErrorMessage = ex.Message;
                        token.Semaphore.Release();
                        continue;
                    }
                }
                _audioDict.TryRemove(new KeyValuePair<string, AudioProcessor>(_userReferenceId, this));
                Console.WriteLine("TryRemove is invoked");
            });
        }
        private bool CheckCapacity(string userReferenceId)
        {
            using var db = new MySqlDbContext(new DbContextOptionsBuilder<MySqlDbContext>().Options);
            var usedCount = (from audio in db.Audios
                             join cloud in db.Clouds
                             on audio.CloudReferenceId equals cloud.CloudReferenceId
                             where cloud.UserReferenceId == userReferenceId
                             where audio.IsCached == "Y"
                             select audio).Count();
            var capacity = (from cloud in db.Clouds
                            where cloud.UserReferenceId == userReferenceId
                            select cloud.Capacity).FirstOrDefault();
            return usedCount < capacity;
        }
    }
}
