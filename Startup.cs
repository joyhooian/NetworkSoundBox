using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NetworkSoundBox.Hubs;

namespace NetworkSoundBox
{
    public class Startup
    {
        public static IServiceCollection _services = null;
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            _services = services;
            services.AddHttpClient();
            services.AddControllers();
            services.AddHostedService<ServerService>();
            services.AddDbContext<MySqlDbContext>(options => options.UseMySql(Configuration.GetConnectionString("MySQL"), MySqlServerVersion.LatestSupportedServerVersion));
            services.AddScoped<IDeviceSvrService, DeviceSvrService>();
            services.AddCors(options =>
            {
                options.AddPolicy("SignalRCors", policy => policy.SetIsOriginAllowed(_ => true)
                                                                 .AllowAnyHeader()
                                                                 .AllowCredentials()
                                                                 .WithMethods("GET", "POST", "HEAD", "PUT", "DELETE", "OPTIONS"));
            });
            services.AddSignalR();
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
                endpoints.MapControllers();
                endpoints.MapHub<NotificationHub>("/NotificationHub").RequireCors("SignalRCors");
            });
        }
    }
}
