﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NetworkSoundBox.Controllers.DTO;
using NetworkSoundBox.Filter;
using NetworkSoundBox.Hubs;
using NetworkSoundBox.Services.Device.Handler;
using NetworkSoundBox.Services.DTO;
using NetworkSoundBox.Services.Message;
using NetworkSoundBox.Services.TextToSpeech;

namespace NetworkSoundBox.Controllers
{
    [Route("api/device_ctrl")]
    [ApiController]
    [ServiceFilter(typeof(ResourceAuthAttribute))]
    public class DeviceController : ControllerBase
    {
        private readonly IDeviceContext _deviceContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IXunfeiTtsService _xunfeiTtsService;
        private readonly INotificationContext _notificationContext;

        public DeviceController(
            INotificationContext notificationContext,
            IXunfeiTtsService xunfeiTtsService,
            IHttpClientFactory httpClientFactory,
            IDeviceContext deviceContext)
        {
            _httpClientFactory = httpClientFactory;
            _deviceContext = deviceContext;
            _xunfeiTtsService = xunfeiTtsService;
            _notificationContext = notificationContext;
        }

        /// <summary>
        /// 获取播放列表
        /// </summary>
        /// <param name="sn">SN</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("play_list")]
        public IActionResult GetPlayList([FromQuery] string sn)
        {
            DeviceHandler device = _deviceContext.DevicePool[sn];
            int result = device.GetPlayList();
            return result != -1 ? Ok(result) : BadRequest("设备未响应");
        }

        /// <summary>
        /// 删除指定音频
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="index">音频序号</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("delete_audio")]
        public IActionResult DeleteAudio([FromQuery] string sn, int index)
        {
            DeviceHandler device = _deviceContext.DevicePool[sn];
            return device.DeleteAudio(index) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 播放指定序号的音频
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="index">音频序号</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("play_index")]
        public IActionResult PlayIndex([FromQuery] string sn, int index)
        {
            DeviceHandler device = _deviceContext.DevicePool[sn];
            return device.PlayIndex(index) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 播放或暂停
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="action">1: 播放; 2: 暂停</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("play_pause")]
        public IActionResult PlayOrPause([FromQuery] string sn, int action)
        {
            DeviceHandler device = _deviceContext.DevicePool[sn];
            return device.SendPlayOrPause(action) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 上一首或下一首
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="action">1: 下一首; 2: 上一首</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("next_previous")]
        public IActionResult NextOrPrevious([FromQuery] string sn, int action)
        {
            DeviceHandler device = _deviceContext.DevicePool[sn];
            return device.SendNextOrPrevious(action) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 设备音量
        /// </summary>
        /// <param name="sn">SN</param>
        /// <param name="volumn">音量(0~30)</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("volumn")]
        public IActionResult Volumn([FromQuery] string sn, int volumn)
        {
            DeviceHandler device = _deviceContext.DevicePool[sn];
            return device.SendVolume(volumn) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 设备重启
        /// </summary>
        /// <param name="sn">SN</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("reboot")]
        public IActionResult Reboot([FromQuery] string sn)
        {
            DeviceHandler device = _deviceContext.DevicePool[sn];
            return device.SendReboot() ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 设备重置
        /// </summary>
        /// <param name="sn"></param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("restore")]
        public IActionResult Restore([FromQuery] string sn)
        {
            DeviceHandler device = _deviceContext.DevicePool[sn];
            return device.SendRestore() ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 批量下发定时任务
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="dtos"></param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("cron_tasks")]
        public IActionResult SetCronTasks([FromQuery] string sn, [FromBody] IList<CronTaskDto> dtos)
        {
            var device = _deviceContext.DevicePool[sn];
            foreach (var dto in dtos)
            {
                // ReSharper disable once CollectionNeverQueried.Local
                List<byte> data = new()
                {
                    (byte) dto.Index,
                    (byte) dto.StartTime.Hour,
                    (byte) dto.StartTime.Minute,
                    (byte) dto.EndTime.Hour,
                    (byte) dto.EndTime.Minute,
                    (byte) dto.Volumn,
                    (byte) (dto.Relay ? 0x01 : 0x00)
                };
                dto.Weekdays.ForEach(d => { data.Add((byte) (d + 1)); });
                data.Add((byte) dto.Audio);
                if (!device.SendCronTask(data)) return BadRequest("设置失败");
            }

            return Ok();
        }

        /// <summary>
        /// 下发定时任务
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="dto"></param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("cron_task")]
        public IActionResult SetAlarms([FromQuery] string sn, [FromBody] CronTaskDto dto)
        {
            DeviceHandler device = _deviceContext.DevicePool[sn];

            List<byte> data = new();
            data.Add((byte) dto.Index);
            data.Add((byte) dto.StartTime.Hour);
            data.Add((byte) dto.StartTime.Minute);
            data.Add((byte) dto.EndTime.Hour);
            data.Add((byte) dto.EndTime.Minute);
            data.Add((byte) dto.Volumn);
            data.Add((byte) (dto.Relay ? 0x01 : 0x00));
            dto.Weekdays.ForEach(d => { data.Add((byte) (d + 1)); });
            data.Add((byte) dto.Audio);

            return device.SendCronTask(data) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 下发延时任务
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="dto"></param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("delay_task")]
        public IActionResult SetAlarmsAfter([FromQuery] string sn, [FromBody] TimeSettingAfterDto dto)
        {
            var device = _deviceContext.DevicePool[sn];

            List<byte> data = new();
            data.Add((byte) ((dto.TimeDelay & 0xFF00) >> 8));
            data.Add((byte) (dto.TimeDelay & 0x00FF));
            data.Add((byte) dto.Volumn);
            data.Add((byte) (dto.Relay ? 0x01 : 0x00));
            data.Add((byte) dto.Audio);

            return device.SendDelayTask(data) ? Ok() : BadRequest("设备未响应");
        }

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="formFile"></param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("file/sn{sn}")]
        public async Task<IActionResult> TransFile(string sn, IFormFile formFile)
        {
            // 读取文件到内存
            byte[] content = new byte[formFile.Length];
            formFile.OpenReadStream().Read(content, 0, content.Length);
            //设置文件名为sn_timestamp.mp3格式，保存在本地硬盘
            var fileName = $"{sn}_{DateTimeOffset.Now.ToUnixTimeSeconds()}.mp3";
            var path = "./Uploaded";
            var fullPath = path + "/" + fileName;
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            FileStream fileStream = new(fullPath, FileMode.CreateNew);
            BinaryWriter binaryWriter = new(fileStream);
            binaryWriter.Write(content);
            binaryWriter.Close();
            fileStream.Close();

            var device = _deviceContext.DevicePool[sn];
            // WiFi 设备传输
            if (device.Type == DeviceType.WiFiTest)
            {
                var fileUploadHandle = new Services.Message.File(content.ToList());
                device.FileQueue.Add(fileUploadHandle);
                content = null;
                fileUploadHandle.Semaphore.WaitOne();
                return fileUploadHandle.FileStatus == FileStatus.Success ? Ok() : BadRequest("文件传输失败");
            }
            // 4G 设备传输
            else
            {
                content = null;
                AudioTransferDto dto = new()
                {
                    Sn = sn,
                    FileName = fileName,
                    FilePath = path,
                    User = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value
                };
                string fileToken = Guid.NewGuid().ToString("N")[..8];
                _deviceContext.AudioDict.Add(fileToken, dto);
                if (!device.ReqFileTrans(Encoding.ASCII.GetBytes(fileToken))) return BadRequest("设备未响应");
                return await dto.Wait() ? Ok() : BadRequest("下载超时");
            }
        }

        /// <summary>
        /// 上传TTS音频到设备
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="text"></param>
        /// <param name="vcn"></param>
        /// <param name="speed"></param>
        /// <param name="volume"></param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("upload_tts")]
        public async Task<IActionResult> UploadTts([FromQuery] string sn, string text, VCN vcn = VCN.XIAOYAN,
            int speed = 50, int volume = 50)
        {
            // 读取文件到内存
            var speech = await _xunfeiTtsService.GetSpeech(text, vcn.ToString().ToLower(), speed, volume, 50);
            byte[] speechContent = new byte[speech.Count];
            speech.CopyTo(speechContent, 0);

            // 写文件到本地硬盘
            var fileName = $"{sn}_{DateTimeOffset.Now.ToUnixTimeSeconds()}.mp3";
            var filePath = "./Tts";
            var fullPath = $"{filePath}/{fileName}";
            if (!Directory.Exists(filePath)) Directory.CreateDirectory(filePath);
            FileStream fileStream = new(fullPath, FileMode.CreateNew);
            BinaryWriter binaryWriter = new(fileStream);
            binaryWriter.Write(speechContent);
            binaryWriter.Close();
            fileStream.Close();
            speechContent = null;

            // 判断设备类型并下发数据
            var device = _deviceContext.DevicePool[sn];
            if (device.Type == DeviceType.WiFiTest)
            {
                var fileUploadHandler = new Services.Message.File(speech);
                device.FileQueue.Add(fileUploadHandler);
                speech = null;
                fileUploadHandler.Semaphore.WaitOne();
                return fileUploadHandler.FileStatus == FileStatus.Success ? Ok() : BadRequest("文件传输失败");
            }
            else if (device.Type == DeviceType.CellularTest)
            {
                AudioTransferDto audioTransferDto = new AudioTransferDto()
                {
                    FileName = fileName,
                    FilePath = filePath,
                    Sn = sn,
                    User = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value
                };
                string fileToken = Guid.NewGuid().ToString("N")[..8];
                _deviceContext.AudioDict.Add(fileToken, audioTransferDto);
                if (!device.ReqFileTrans(Encoding.ASCII.GetBytes(fileToken))) return BadRequest("设备未响应");
                return await audioTransferDto.Wait() ? Ok() : BadRequest("下载超时");
            }

            return BadRequest("不支持该设备类型");
        }

        /// <summary>
        /// 设备调用，下载文件
        /// </summary>
        [HttpGet("download_file")]
        public IActionResult Download_File([FromQuery] string fileToken)
        {
            if (_deviceContext.FileList.TryGetValue(fileToken, out var filePair))
            {
                FileContentResult file = new(filePair.Value.FileContents, "audio/mp3");
                filePair.Key.Release();
                return file;
            }
            else
            {
                return BadRequest("非法参数");
            }
        }

        /// <summary>
        /// 流式传输文件
        /// </summary>
        /// <param name="fileToken"></param>
        /// <returns></returns>
        [HttpGet("download_file_stream")]
        public async Task<IActionResult> DownloadFileStream([FromQuery] string fileToken)
        {
            if (_deviceContext.AudioDict.TryGetValue(fileToken, out AudioTransferDto audioHandler))
            {
                try
                {
                    FileInfo fileInfo = new($"{audioHandler.FilePath}/{audioHandler.FileName}");
                    byte[] contentBuffer = new byte[fileInfo.Length];
                    fileInfo.OpenRead().Read(contentBuffer, 0, contentBuffer.Length);

                    var contentDisposition = $"attachment;filename={HttpUtility.UrlEncode(fileInfo.Name)}";
                    Response.Headers.Add("Content-Disposition", new string[] {contentDisposition});
                    Response.ContentType = "audio/mp3";
                    Response.ContentLength = contentBuffer.Length;
                    using (Response.Body)
                    {
                        int hasSent = 0;
                        int sendLength = 0;

                        while (hasSent < contentBuffer.Length)
                        {
                            if (HttpContext.RequestAborted.IsCancellationRequested) break;
                            sendLength = contentBuffer.Length - hasSent < 1024 ? contentBuffer.Length - hasSent : 1024;
                            ReadOnlyMemory<byte> readOnlyMemory = new(contentBuffer, hasSent, sendLength);
                            await Response.Body.WriteAsync(readOnlyMemory);
                            hasSent += sendLength;
                            await _notificationContext.SendDownloadProgress(audioHandler.User,
                                100.0f * hasSent / contentBuffer.Length);
                            await Response.Body.FlushAsync();
                        }

                        if (hasSent == contentBuffer.Length)
                            audioHandler.TransferCplt(true);
                        else
                            audioHandler.TransferCplt(false);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    _deviceContext.AudioDict.Remove(fileToken);
                    audioHandler.TransferCplt(false);
                }
            }

            return new EmptyResult();
        }

        /// <summary>
        /// 流式传输测试接口
        /// </summary>
        /// <param name="fileToken"></param>
        /// <returns></returns>
        [HttpGet("download_file_stream_test")]
        public async Task<IActionResult> DownloadFileStreamTest([FromQuery] string fileToken)
        {
            if (fileToken == "12345678")
            {
                try
                {
                    FileInfo fileInfo = new($"./Uploaded/test.mp3");
                    byte[] contentBuffer = new byte[fileInfo.Length];
                    fileInfo.OpenRead().Read(contentBuffer, 0, contentBuffer.Length);
                    var contentDisposition = $"attachment;filename={HttpUtility.UrlEncode(fileInfo.Name)}";
                    Response.Headers.Add("Content-Disposition", new string[] {contentDisposition});
                    Response.ContentType = "audio/mp3";
                    Response.ContentLength = contentBuffer.Length;
                    using (Response.Body)
                    {
                        int hasSent = 0;
                        int sendLength = 0;
                        while (hasSent < contentBuffer.Length)
                        {
                            if (HttpContext.RequestAborted.IsCancellationRequested) break;
                            sendLength = contentBuffer.Length - hasSent < 1024 ? contentBuffer.Length - hasSent : 1024;
                            ReadOnlyMemory<byte> readOnlyMemory = new(contentBuffer, hasSent, sendLength);
                            await Response.Body.WriteAsync(readOnlyMemory);
                            hasSent += sendLength;
                            await Response.Body.FlushAsync();
                        }
                    }
                }
                catch (Exception)
                {
                }

                return new EmptyResult();
            }

            return BadRequest();
        }
    }
}