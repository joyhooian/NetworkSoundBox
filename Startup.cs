using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NetworkSoundBox.Hubs;
using NetworkSoundBox.Authorization.Policy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using NetworkSoundBox.Authorization;
using NetworkSoundBox.WxAuthorization.AccessToken;
using NetworkSoundBox.WxAuthorization.QRCode;
using NetworkSoundBox.Authorization.WxAuthorization.Login;
using NetworkSoundBox.Services.TextToSpeech;

namespace NetworkSoundBox
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            string issuer = Configuration["Jwt:Issuer"];
            string audience = Configuration["Jwt:Audience"];
            string expire = Configuration["Jwt:ExpireMinutes"];
            TimeSpan expiration = TimeSpan.FromMinutes(Convert.ToDouble(expire));
            SecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["Jwt:SecurityKey"]));

            services.AddAuthorization(options =>
            {
                options.AddPolicy("Permission",
                    policy => policy.Requirements.Add(new PolicyRequirement()));
            }).AddAuthentication(s =>
            {
                s.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                s.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                s.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(s =>
            {
                s.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = key,
                    ClockSkew = expiration,
                    ValidateLifetime = true
                };
                s.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                        {
                            context.Response.Headers.Add("Token-Expired", "true");
                        }
                        return Task.CompletedTask;
                    }
                };
            });
            services.AddTransient<IJwtAppService, JwtAppService>();
            services.AddSingleton<IAuthorizationHandler, PolicyHandler>();
            services.AddSingleton<IWxAccessService, WxAccessService>();
            services.AddSingleton<IWxLoginQRService, WxLoginQRService>();
            services.AddSingleton<IDeviceSvrService, DeviceSvrService>();
            services.AddSingleton<IWxLoginService, WxLoginService>();
            services.AddScoped<IXunfeiTtsService, XunfeiTtsService>();

            services.AddSignalR();
            services.AddHttpClient();
            services.AddControllers();
            services.AddHttpContextAccessor();
            services.AddHostedService<ServerService>();
            services.AddDbContext<MySqlDbContext>(options => options.UseMySql(Configuration.GetConnectionString("MySQL"), MySqlServerVersion.LatestSupportedServerVersion));
            services.AddCors(options =>
            {
                options.AddPolicy("SignalRCors", policy => policy.SetIsOriginAllowed(_ => true)
                                                                 .AllowAnyHeader()
                                                                 .AllowCredentials()
                                                                 .WithMethods("GET", "POST", "HEAD", "PUT", "DELETE", "OPTIONS"));
            });
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "NetworkSoundBox", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "NetworkSoundBox v1"));
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseCors("SignalRCors");

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers().RequireCors("SignalRCors");
                endpoints.MapHub<NotificationHub>("/NotificationHub").RequireCors("SignalRCors");
            });
        }
    }
}
