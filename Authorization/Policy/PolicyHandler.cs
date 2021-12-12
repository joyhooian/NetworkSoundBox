using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using NetworkSoundBox.Authorization.Jwt;

namespace NetworkSoundBox.Authorization.Policy
{
    internal class PolicyHandler : AuthorizationHandler<PolicyRequirement>
    {
        public IAuthenticationSchemeProvider Schemes { get; set; }

        private readonly IJwtAppService _jwtApp;

        public PolicyHandler(IAuthenticationSchemeProvider schemes, IJwtAppService jwtApp)
        {
            Schemes = schemes;
            _jwtApp = jwtApp;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PolicyRequirement requirement)
        {
            var defaultAuthenticate = await Schemes.GetDefaultAuthenticateSchemeAsync();

            if (defaultAuthenticate != null)
            {
                if (context.User.Identity.IsAuthenticated)
                {
                    if (!_jwtApp.IsCurrentActiveToken())
                    {
                        context.Fail();
                        return;
                    }
                    return;
                }
            }
            context.Fail();
        }
    }
}
