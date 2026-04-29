using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using DDDExample.Domain.Entities;
using DDDExample.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace DDDExample.Middleware
{
    public class ResponseTimeMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ResponseTimeMiddleware> _logger;
        private readonly ResponseTimeOptions _options;

        public ResponseTimeMiddleware(
            RequestDelegate next, 
            ILogger<ResponseTimeMiddleware> logger,
            IOptions<ResponseTimeOptions> options)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options.Value;
        }

        public async Task InvokeAsync(HttpContext context, IResponseTimeLogRepository logRepository)
        {
            if (context.Request.Path.StartsWithSegments("/health") || 
                context.Request.Path.StartsWithSegments("/swagger") ||
                context.Request.Path.StartsWithSegments("/_framework") ||
                context.Request.Path.StartsWithSegments("/favicon.ico"))
            {
                await _next(context);
                return;
            }

            var watch = Stopwatch.StartNew();
            
            try
            {
                context.Response.OnStarting(state =>
                {
                    var httpContext = (HttpContext)state;
                    httpContext.Response.Headers["X-Response-Time"] = $"{watch.ElapsedMilliseconds}ms";
                    return Task.CompletedTask;
                }, context);

                await _next(context);
                
                watch.Stop();
                var responseTime = watch.ElapsedMilliseconds;
                var isSlow = responseTime > _options.ThresholdMs;

                if (_options.LogSlowRequests && isSlow)
                {
                    _logger.LogWarning($"Slow Request Detected: {context.Request.Method} {context.Request.Path} took {responseTime}ms");
                }
                else
                {
                    _logger.LogInformation($"Request: {context.Request.Method} {context.Request.Path} - {responseTime}ms");
                }

                try
                {
                    var logEntry = new ResponseTimeLog
                    {
                        Path = context.Request.Path,
                        Method = context.Request.Method,
                        QueryString = context.Request.QueryString.Value,
                        DurationMs = responseTime,
                        StatusCode = context.Response.StatusCode,
                        ClientIp = context.Connection.RemoteIpAddress?.ToString(),
                        UserAgent = context.Request.Headers["User-Agent"],
                        IsSlowRequest = isSlow,
                        Timestamp = DateTime.UtcNow
                    };

                    _ = logRepository.AddAsync(logEntry).ContinueWith(t => 
                    {
                        if (t.IsFaulted)
                        {
                            _logger.LogError(t.Exception, "Failed to log response time to MongoDB");
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error logging response time");
                }
            }
            catch (Exception ex)
            {
                watch.Stop();
                _logger.LogError(ex, "Error processing request {Method} {Path} after {Elapsed}ms",
                    context.Request.Method,
                    context.Request.Path,
                    watch.ElapsedMilliseconds);
                
                if (!context.Response.HasStarted)
                {
                    throw;
                }
                
                _logger.LogWarning("Could not handle error properly because response has already started.");
            }
        }   }
}