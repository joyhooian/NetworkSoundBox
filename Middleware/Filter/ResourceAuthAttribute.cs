using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using NetworkSoundBox.Services.Device.Handler;
using System;
using Microsoft.AspNetCore.Http;
using NetworkSoundBox.Entities;
using System.Linq;

namespace NetworkSoundBox.Middleware.Filter
{
    [Serializable, AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class ResourceAuthAttribute : ActionFilterAttribute
    {
        private readonly IDeviceContext _deviceContext;
        private readonly MySqlDbContext _dbContext;

        public ResourceAuthAttribute(
            MySqlDbContext dbContext,
            IDeviceContext deviceContext)
        {
            _deviceContext = deviceContext;
            _dbContext = dbContext;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            #region sn验证
            if (context.ActionArguments.TryGetValue("sn", out var sn))
            {
                if (string.IsNullOrEmpty(sn as string) ||
                    !_deviceContext.DevicePool.ContainsKey(sn as string))
                {
                    context.Result = new BadRequestObjectResult("无效SN或设备不在线");
                    base.OnActionExecuting(context);
                }
            }
            #endregion
            #region action验证
            if (context.ActionArguments.TryGetValue("action", out var action))
            {
                if ((int)action != 0 && (int)action != 1) context.Result = new BadRequestObjectResult("非法参数");
                base.OnActionExecuting(context);
            }
            #endregion
            #region volumn验证
            if (context.ActionArguments.TryGetValue("volumn", out var volumn))
            {
                if ((int)volumn < 0 || (int)volumn > 100) context.Result = new BadRequestObjectResult("非法参数");
                base.OnActionExecuting(context);
            }
            #endregion
            #region formFile验证
            if (context.ActionArguments.TryGetValue("formFile", out var formFile))
            {
                if (formFile is not IFormFile file) context.Result = new BadRequestObjectResult("非法文件");
                else if (file.Length > 1024 * 1024 * 50) context.Result = new BadRequestObjectResult("文件过大");
                else if (file.ContentType != "audio/mpeg") context.Result = new BadRequestObjectResult("文件格式错误");
                base.OnActionExecuting(context);
            }
            #endregion
            base.OnActionExecuting(context);
        }
    }
}
