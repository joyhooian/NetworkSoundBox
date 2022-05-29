using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NetworkSoundBox.Controllers.Model.Request;
using NetworkSoundBox.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace NetworkSoundBox.Controllers
{
    [Route("api/task")]
    [ApiController]
    public class TaskController : ControllerBase
    {
        private readonly MySqlDbContext _dbContext;
        private readonly ILogger<TaskController> _logger;

        private string UserReferenceId { get => GetUser(); }

        public TaskController(
            MySqlDbContext dbContext,
            ILogger<TaskController> logger)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        [Authorize(Policy = "Permission")]
        [HttpPost("add_cron")]
        public IActionResult AddCronTask([FromBody] AddCronTaskRequest request)
        {
            if (!TryParseWeekdays(request?.Weekdays, out var weekDays))
                return BadRequest("星期参数有误");

            if (!TryParseTime(request.StartTime, request.EndTime, out string startTime, out string endTime)) 
                return BadRequest("时间参数有误");

            if (request.Volume < 0 || request.Volume > 30) 
                return BadRequest("音量参数有误");

            if (request.Relay != 0 && request.Relay != 1) 
                return BadRequest("继电器参数有误");

            if (string.IsNullOrEmpty(request?.AudioReferenceId))
                return BadRequest("音频参数有误");

            try
            {
                var audioEntity = (from audio in _dbContext.Audios
                                   join cloud in _dbContext.Clouds
                                   on audio.CloudReferenceId equals cloud.CloudReferenceId
                                   where cloud.UserReferenceId == UserReferenceId
                                   where audio.AudioReferenceId == request.AudioReferenceId
                                   select audio).FirstOrDefault();
                if (audioEntity == null)
                    return BadRequest("无此音频");

                var cronEntity = new CronTask()
                {
                    CronReferenceId = Guid.NewGuid().ToString(),
                    Weekdays = weekDays,
                    StartTime = startTime,
                    EndTime = endTime,
                    Volume = request.Volume,
                    Relay = request.Relay,
                    AudioReferenceId = audioEntity.AudioReferenceId,
                    UserReferenceId = UserReferenceId
                };
                _dbContext.CronTasks.Add(cronEntity);
                _dbContext.SaveChanges();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While AddCronTask is invoked");
                return BadRequest(ex.Message);
            }
        }

        [Authorize(Policy = "Permission")]
        [HttpPost("add_delay")]
        public IActionResult AddDelayTask([FromBody] AddDelayTaskRequest request)
        {
            if (request.DelayTime < 0 || request.DelayTime > 60000)
                return BadRequest("延迟时间有误");

            if (request.Relay != 0 && request.Relay != 1)
                return BadRequest("继电器状态有误");

            if (request.Volume < 0 || request.Volume > 30)
                return BadRequest("音量参数有误");

            if (string.IsNullOrEmpty(request.AudioReferenceId))
                return BadRequest("音频参数有误");

            try
            {
                var audioEntity = (from audio in _dbContext.Audios
                                   join cloud in _dbContext.Clouds
                                   on audio.CloudReferenceId equals cloud.CloudReferenceId
                                   where cloud.UserReferenceId == UserReferenceId
                                   where audio.AudioReferenceId == request.AudioReferenceId
                                   select audio).FirstOrDefault();
                if (audioEntity == null)
                    return BadRequest("无此音频");

                var delayTaskEntity = new DelayTask()
                {
                    DelayReferenceId = Guid.NewGuid().ToString(),
                    DelayTime = request.DelayTime,
                    Volume = request.Volume,
                    Relay = request.Relay,
                    AudioReferenceId = audioEntity.AudioReferenceId,
                    UserReferenceId = UserReferenceId
                };
                _dbContext.DelayTasks.Add(delayTaskEntity);
                _dbContext.SaveChanges();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While AddDelayTask is invoked");
                return BadRequest(ex.Message);
            }
        }

        [Authorize(Policy = "Permission")]
        [HttpPost("del_crontask")]
        public IActionResult DeleteCronTask([FromQuery] string taskReferenceId)
        {
            if (string.IsNullOrEmpty(taskReferenceId))
                return BadRequest("请求为空");

            try
            {
                var cronTaskEntity = (from cron in _dbContext.CronTasks
                                      where cron.UserReferenceId == UserReferenceId
                                      where cron.CronReferenceId == taskReferenceId
                                      select cron).FirstOrDefault();

                if (cronTaskEntity == null)
                    return Ok();

                var deviceTaskEntities = (from deviceTask in _dbContext.DeviceTasks
                                          join userDevice in _dbContext.UserDevices
                                          on deviceTask.DeviceReferenceId equals userDevice.DeviceRefrenceId
                                          where userDevice.UserRefrenceId == UserReferenceId
                                          where deviceTask.TaskReferenceId == cronTaskEntity.CronReferenceId
                                          select deviceTask).ToList();
                if (deviceTaskEntities != null && deviceTaskEntities.Count > 0)
                    return BadRequest("无法删除, 此定时仍在使用");
                else
                {
                    _dbContext.CronTasks.Remove(cronTaskEntity);
                    _dbContext.SaveChanges();
                }
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While DeleteTask is invoked");
                return BadRequest(ex.Message);
            }
        }

        [Authorize(Policy = "Permission")]
        [HttpPost("del_delaytask")]
        public IActionResult DeleteDelayTask([FromQuery] string taskReferenceId)
        {
            if (string.IsNullOrEmpty(taskReferenceId))
                return BadRequest("请求为空");

            try
            {
                var delayTaskEntity = (from cron in _dbContext.DelayTasks
                                  where cron.UserReferenceId == UserReferenceId
                                  where cron.DelayReferenceId == taskReferenceId
                                  select cron).FirstOrDefault();

                if (delayTaskEntity == null)
                    return Ok();

                var deviceTaskEntities = (from deviceTask in _dbContext.DeviceTasks
                                          join userDevice in _dbContext.UserDevices
                                          on deviceTask.DeviceReferenceId equals userDevice.DeviceRefrenceId
                                          where userDevice.UserRefrenceId == UserReferenceId
                                          where deviceTask.TaskReferenceId == delayTaskEntity.DelayReferenceId
                                          select deviceTask).ToList();
                if (deviceTaskEntities != null && deviceTaskEntities.Count > 0)
                    return BadRequest("无法删除, 此定时仍在使用");
                else
                {
                    _dbContext.DelayTasks.Remove(delayTaskEntity);
                    _dbContext.SaveChanges();
                }
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While DeleteTask is invoked");
                return BadRequest(ex.Message);
            }
        }

        [Authorize(Policy = "Permission")]
        [HttpPost("get_tasks")]
        public IActionResult GetTasks()
        {
            var cronTaskEntities = (from cronTaskEntity in _dbContext.CronTasks
                                    where cronTaskEntity.UserReferenceId == UserReferenceId
                                    select cronTaskEntity).ToList();

            var delayTaskEntities = (from delayTaskEntity in _dbContext.DelayTasks
                                     where delayTaskEntity.UserReferenceId == UserReferenceId
                                     select delayTaskEntity).ToList();

            return Ok(JsonConvert.SerializeObject(new { CronTasks = cronTaskEntities, DelayTasks = delayTaskEntities }));
        }

        [Authorize(Policy = "Permission")]
        [HttpPost("update_crontask")]
        public IActionResult UpdateCronTask([FromBody] UpdateCronTaskRequest request)
        {
            if (!TryParseWeekdays(request?.Weekdays, out var weekDays))
                return BadRequest("星期参数有误");

            if (!TryParseTime(request.StartTime, request.EndTime, out string startTime, out string endTime))
                return BadRequest("时间参数有误");

            if (request.Volume < 0 || request.Volume > 30)
                return BadRequest("音量参数有误");

            if (request.Relay != 0 && request.Relay != 1)
                return BadRequest("继电器参数有误");

            if (string.IsNullOrEmpty(request?.AudioReferenceId))
                return BadRequest("音频参数有误");

            try
            {
                var cronTaskEntity = (from cronTask in _dbContext.CronTasks
                                      where cronTask.UserReferenceId == UserReferenceId
                                      where cronTask.CronReferenceId == request.CronReferenceId
                                      select cronTask).FirstOrDefault();
                if (cronTaskEntity == null)
                    return BadRequest("参数有误");

                var audioEntity = (from audio in _dbContext.Audios
                                   join cloud in _dbContext.Clouds
                                   on audio.CloudReferenceId equals cloud.CloudReferenceId
                                   where cloud.UserReferenceId == UserReferenceId
                                   where audio.AudioReferenceId == request.AudioReferenceId
                                   select audio).FirstOrDefault();
                if (audioEntity == null)
                    return BadRequest("音频参数有误");

                cronTaskEntity.Volume = request.Volume;
                cronTaskEntity.Relay = request.Relay;
                cronTaskEntity.Weekdays = weekDays;
                cronTaskEntity.StartTime = startTime;
                cronTaskEntity.EndTime = endTime;
                cronTaskEntity.AudioReferenceId = request.AudioReferenceId;
                _dbContext.CronTasks.Update(cronTaskEntity);
                _dbContext.SaveChanges();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While UpdateCronTask is invoked");
                return BadRequest(ex.Message);
            }
        }

        [Authorize(Policy = "Permission")]
        [HttpPost("update_delaytask")]
        public IActionResult UpdateDelayTask([FromBody] UpdateDelayTaskRequest request)
        {
            if (request.DelayTime < 0 || request.DelayTime > 60000)
                return BadRequest("延迟时间有误");

            if (request.Relay != 0 && request.Relay != 1)
                return BadRequest("继电器状态有误");

            if (request.Volume < 0 || request.Volume > 30)
                return BadRequest("音量参数有误");

            if (string.IsNullOrEmpty(request.AudioReferenceId))
                return BadRequest("音频参数有误");

            try
            {
                var delayTaskEntity = (from delayTask in _dbContext.DelayTasks
                                       where delayTask.UserReferenceId == UserReferenceId
                                       where delayTask.DelayReferenceId == request.DelayReferenceId
                                       select delayTask).FirstOrDefault();
                if (delayTaskEntity == null)
                    return BadRequest("参数有误");

                var audioEntity = (from audio in _dbContext.Audios
                                   join cloud in _dbContext.Clouds
                                   on audio.CloudReferenceId equals cloud.CloudReferenceId
                                   where cloud.UserReferenceId == UserReferenceId
                                   where audio.AudioReferenceId == request.AudioReferenceId
                                   select audio).FirstOrDefault();
                if (audioEntity == null)
                    return BadRequest("音频参数有误");

                delayTaskEntity.AudioReferenceId = request.AudioReferenceId;
                delayTaskEntity.Relay = request.Relay;
                delayTaskEntity.Volume = request.Volume;
                delayTaskEntity.DelayTime = request.DelayTime;

                _dbContext.DelayTasks.Update(delayTaskEntity);
                _dbContext.SaveChanges();

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While UpdateDelayTask is invoked");
                return BadRequest(ex.Message);
            }
        }

        private static bool TryParseWeekdays(string weekdaysStr, out string parsedWeekdays)
        {
            if (string.IsNullOrEmpty(weekdaysStr))
            {
                parsedWeekdays = null;
                return false;
            }

            List<string> tempWeekdays = new();

            weekdaysStr = weekdaysStr.Trim();
            tempWeekdays = weekdaysStr.Split(',').ToList();
            if (tempWeekdays == null || tempWeekdays.Count == 0)
            {
                parsedWeekdays = null;
                return false;
            }

            if (tempWeekdays.Any(w => w.Length != 1 || !int.TryParse(w, out int wInt) || wInt < 1 || wInt > 7 ))
            {
                parsedWeekdays = null;
                return false;
            }

            tempWeekdays.Sort((a, b) => int.Parse(a).CompareTo(int.Parse(b)));
            var sb = new StringBuilder();
            for (int cnt = 0; cnt < tempWeekdays.Count; cnt++)
            {
                if (cnt > 0)
                {
                    if (tempWeekdays[cnt] == tempWeekdays[cnt - 1])
                    {
                        parsedWeekdays = null;
                        return false;
                    }
                }
                sb.Append(tempWeekdays[cnt]);
                if (cnt < tempWeekdays.Count - 1)
                {
                    sb.Append(',');
                }
            }
            parsedWeekdays = sb.ToString();
            return true;
        }

        private static bool TryParseTime(string startTime, string endTime, out string startTimeStr, out string endTimeStr)
        {
            startTimeStr = null;
            endTimeStr = null;

            if (!startTime.Contains(':') || !endTime.Contains(':')) return false;

            if (!int.TryParse(startTime.Split(':').FirstOrDefault(), out int startHour)) return false;

            if (!int.TryParse(startTime.Split(':').LastOrDefault(), out int startMinute)) return false;

            if (!int.TryParse(endTime.Split(':').FirstOrDefault(), out int endHour)) return false;

            if (!int.TryParse(endTime.Split(':').LastOrDefault(), out int endMinute)) return false;

            if (startHour < endHour || (startHour == endHour && startMinute < endMinute))
            {
                startTimeStr = $"{startHour}:{startMinute}";
                endTimeStr = $"{endHour}:{endMinute}";
                return true;
            }
            return false;
        }

        private string GetUser()
        {
            return HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
        }
    }
}
