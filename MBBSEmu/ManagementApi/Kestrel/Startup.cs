using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NLog;

namespace MBBSEmu.ManagementApi.Kestrel
{
    public class Startup
    {
        private protected static ILogger _logger => DependencyInjection.ServiceResolver.GetService<ILogger>();
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            if (string.IsNullOrEmpty(configuration["ManagementAPI.Secret"]))
            {
                configuration["ManagementAPI.Secret"] = Guid.NewGuid().ToString();
                _logger.Warn("To set your own Management API Secret, add the value ManagementAPI.Secret to appsettings.json");
            }
            else if(configuration["ManagementAPI.Secret"].Length < 16)

            {
                configuration["ManagementAPI.Secret"] = Guid.NewGuid().ToString();
                _logger.Warn("ManagementAPI.Secret in appsettings.json is too short, it must be at least 16 characters long.");
            }

            _logger.Warn($"Generated new ManagementAPI.Secret: {configuration["ManagementAPI.Secret"]}");
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddResponseCompression();

            services.AddAuthentication(x =>
                {
                    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(x =>
                {
                    x.RequireHttpsMetadata = false;
                    x.SaveToken = false;
                    x.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey =
                            new SymmetricSecurityKey(Encoding.ASCII.GetBytes(Configuration["ManagementAPI.Secret"])),
                        ValidateIssuer = true,
                        ValidIssuer = "MBBSEmu Management API",
                        ValidateAudience = false
                    };
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            
            app.UseRouting();

            
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseResponseCompression();
        }
    }
}
