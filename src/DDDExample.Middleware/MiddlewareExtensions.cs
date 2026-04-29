using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using DDDExample.Domain.Repositories;
using DDDExample.Infrastructure.Repositories.MongoDB;
using Microsoft.Extensions.Configuration;
using DDDExample.Infrastructure.Persistence.MongoDB;

namespace DDDExample.Middleware
{
    public static class MiddlewareExtensions
    {
        public static IServiceCollection AddResponseTimeOptions(this IServiceCollection services, 
            Action<ResponseTimeOptions> configureOptions, IConfiguration configuration)
        {
            services.Configure<MongoDbSettings>(configuration.GetSection("MongoDBSettings"));

            // Register MongoDB repository as singleton since it's used in middleware
            services.AddSingleton<IResponseTimeLogRepository, MongoResponseTimeLogRepository>();

            services.Configure(configureOptions);
            return services;
        }

        public static IApplicationBuilder UseResponseTimeMiddleware(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ResponseTimeMiddleware>();
        }
    }

    public class ResponseTimeOptions
    {
        public long ThresholdMs { get; set; } = 500;
        public bool LogSlowRequests { get; set; } = true;
    }
}