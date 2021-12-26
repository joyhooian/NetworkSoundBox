using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NetworkSoundBox.Authorization.Device;
using NetworkSoundBox.Authorization.Jwt;
using NetworkSoundBox.Authorization.Policy;
using NetworkSoundBox.Authorization.WxAuthorization.AccessToken;
using NetworkSoundBox.Authorization.WxAuthorization.Login;
using NetworkSoundBox.Authorization.WxAuthorization.QRCode;
using NetworkSoundBox.AutoMap;
using NetworkSoundBox.Entities;
using NetworkSoundBox.Filter;
using NetworkSoundBox.Hubs;
using NetworkSoundBox.Services.Device.Handler;
using NetworkSoundBox.Services.Device.Server;
using NetworkSoundBox.Services.TextToSpeech;

namespace NetworkSoundBox
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        private IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            #region ÃÌº”Jwtº¯»®≈‰÷√
            #region Jwt≈‰÷√◊÷∑˚¥Æ
            var issuer = Configuration["Jwt:Issuer"];
            var audience = Configuration["Jwt:Audience"];
            var expire = Configuration["Jwt:ExpireMinutes"];
            var expiration = TimeSpan.FromMinutes(Convert.ToDouble(expire));
            SecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["Jwt:SecurityKey"])); 
            #endregion
            services.AddAuthorization(options =>
                {
                    options.AddPolicy("Permission",
                        policy => policy.Requirements.Add(new PolicyRequirement()));
                })
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = issuer,
                        ValidAudience = audience,
                        IssuerSigningKey = key,
                        ClockSkew = expiration,
                        ValidateLifetime = true
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                            {
                                context.Response.Headers.Add("Token-Expired", "true");
                            }
                            return Task.CompletedTask;
                        },
                        #region ≈‰÷√SignalRº¯»®
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrEmpty(accessToken) &&
                                path.StartsWithSegments("/NotificationHub"))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        } 
                        #endregion
                    };
                }); 
            #endregion
            services.AddTransient<IJwtAppService, JwtAppService>();
            services.AddSingleton<IAuthorizationHandler, PolicyHandler>();
            services.AddSingleton<IWxAccessService, WxAccessService>();
            services.AddSingleton<IWxLoginQRService, WxLoginQRService>();
            services.AddSingleton<IDeviceContext, DeviceContext>();
            services.AddSingleton<IWxLoginService, WxLoginService>();
            services.AddSingleton<IDeviceAuthorization, DeviceAuthorization>();
            services.AddSingleton<INotificationContext, NotificationContext>();
            services.AddScoped<IXunfeiTtsService, XunfeiTtsService>();
            services.AddScoped<ResourceAuthAttribute>();

            services.AddAutoMapper(typeof(AutoMapperProfile));
            services.AddSignalR();
            services.AddHttpClient();
            services.AddControllers();
            services.AddHttpContextAccessor();
            services.AddHostedService<ServerService>();
            services.AddDbContext<MySqlDbContext>(options => options.UseMySql(Configuration.GetConnectionString("MySQL"), MySqlServerVersion.LatestSupportedServerVersion));
            services.AddCors(options =>
            {
                options.AddPolicy("DefaultCors", policy => policy.SetIsOriginAllowed(_ => true)
                                                                 .AllowAnyHeader()
                                                                 .AllowCredentials()
                                                                 .WithMethods("GET", "POST", "HEAD", "PUT", "DELETE", "OPTIONS"));
            });
            #region ÃÌº”Swagger ”Õº
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
            #endregion
            #region ÃÌº”NewtonJson–Ú¡–ªØ≈‰÷√
            Newtonsoft.Json.JsonConvert.DefaultSettings = new Func<Newtonsoft.Json.JsonSerializerSettings>(() =>
            {
                return new Newtonsoft.Json.JsonSerializerSettings
                {
                    DateFormatHandling = Newtonsoft.Json.DateFormatHandling.MicrosoftDateFormat,
                    DateFormatString = "yyyy/MM/dd HH:mm",

                    ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
                };
            });
            #endregion
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "NetworkSoundBox v1"));

            app.UseRouting();
            app.UseCors("DefaultCors");


            app.UseAuthentication();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers().RequireCors("DefaultCors");
                endpoints.MapHub<NotificationHub>("/NotificationHub").RequireCors("DefaultCors");
            });
        }
    }
}
