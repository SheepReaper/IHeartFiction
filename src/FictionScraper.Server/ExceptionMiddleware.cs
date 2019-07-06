using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using FictionScraper.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace FictionScraper.Server
{
    public class ExceptionMiddleware
    {
        private readonly string _defaultResponseMediaType = MediaTypeNames.Application.Json;
        private readonly string[] _supportedMediaTypes =
            { MediaTypeNames.Application.Json, MediaTypeNames.Application.Xml };

        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;
        private bool DeveloperModeEnabled;

        public ExceptionMiddleware(RequestDelegate next, ILoggerFactory logger, IWebHostEnvironment env)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger?.CreateLogger<ExceptionMiddleware>() ?? throw new ArgumentNullException(nameof(logger));
            DeveloperModeEnabled = env.IsDevelopment();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            Console.Error.WriteLine("\n\nEHM Invocation\n\n");
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                if (context.Response.HasStarted)
                {
                    _logger.LogWarning("The response has already started, the exception handler middleware will not execute.");
                    throw;
                }

                context.Response.Clear();
                _logger.LogError(ex, $"Something broke: {ex.Message}", null);
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception,
            CancellationToken ctx = default)
        {

            Console.Error.WriteLine("\n\nInHandleExceptionAsync\n\n");
            var defaultResponseMediaType = MediaTypeNames.Application.Json;
            var requestedResponseMediaType = MediaTypeNames.Application.Json;

            var supported = false;

            foreach (var mediaType in _supportedMediaTypes)
            {
                supported = ((string)context.Request.Headers["Accept"]).Contains(mediaType);
                requestedResponseMediaType = mediaType;
                break;
            }

            context.Response.ContentType = requestedResponseMediaType;
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            if (supported)
            {
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new ErrorResponse { Message = "Default error message" }), ctx);
                //await context.Response.WriteAsync((new ErrorResponse { Message = "Default error message" }), ctx);
            }
            else
            {
            }

        }
    }
}
