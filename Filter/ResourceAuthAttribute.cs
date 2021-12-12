using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using NetworkSoundBox.Services.Device.Handler;
using System;

namespace NetworkSoundBox.Filter
{
    [Serializable, AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class ResourceAuthAttribute : ActionFilterAttribute
    {
        private readonly IDeviceContext _deviceContext;

        public ResourceAuthAttribute(
            IDeviceContext deviceContext)
        {
            _deviceContext = deviceContext;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionArguments.TryGetValue("sn", out var sn))
            {
                try
                {
                    var device = _deviceContext.DeviceDict[(string)sn];
                    if (device.UserOpenId != context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value)
                    {
                        throw new Exception();
                    }
                }
                catch(Exception)
                {
                    context.Result = new BadRequestObjectResult("无效SN或设备不在线");
                }
            }
            base.OnActionExecuting(context);
        }
    }
}
