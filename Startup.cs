using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
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
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using NetworkSoundBox.Entities;
using AutoMapper;
using NetworkSoundBox.AutoMap;
using NetworkSoundBox.Authorization.Device;
using NetworkSoundBox.Services.Device.Handler;
using NetworkSoundBox.Services.Device.Server;

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
            services.AddSingleton<IDeviceContext, DeviceContext>();
            services.AddSingleton<IWxLoginService, WxLoginService>();
            services.AddSingleton<IDeviceAuthorization, DeviceAuthorization>();
            services.AddScoped<IXunfeiTtsService, XunfeiTtsService>();

            services.AddAutoMapper(typeof(AutoMapperProfile));
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
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",//Jwt default param name
                    In = ParameterLocation.Header,//Jwt store address
                    Type = SecuritySchemeType.ApiKey//Security scheme type
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            },
                            Scheme = "oauth2",
                            Name = "Bearer",
                            In = ParameterLocation.Header,
                        },
                        new List<string>()
                    }
                });
            });

            Newtonsoft.Json.JsonConvert.DefaultSettings = new Func<Newtonsoft.Json.JsonSerializerSettings>(() =>
            {
                return new Newtonsoft.Json.JsonSerializerSettings
                {
                    DateFormatHandling = Newtonsoft.Json.DateFormatHandling.MicrosoftDateFormat,
                    DateFormatString = "yyyy/MM/dd HH:mm",

                    ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
                };
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
            app.UseCors("SignalRCors");


            app.UseAuthentication();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers().RequireCors("SignalRCors");
                endpoints.MapHub<NotificationHub>("/NotificationHub").RequireCors("SignalRCors");
            });
        }
    }
}
