using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Blazor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Reflection;

namespace FictionScraper.Server
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // Generic
            services
                .AddResponseCompression(opts =>
                {
                    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                        new[] { MediaTypeNames.Application.Octet });
                })
                .AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("AppDb"))
                .AddSwaggerGen(options =>
                {
                    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

                    options.SwaggerDoc("doc", new OpenApiInfo
                    {
                        Contact = new OpenApiContact
                        {
                            Email = "bgonza868@gmail.com",
                            Name = "Bryan Gonzalez",
                            Url = new Uri("https://www.sheepreaper.com")
                        },
                        Description = "Fanfiction Aggregation and manipulation API",
                        License = new OpenApiLicense
                        { Name = "GNU GPLv3", Url = new Uri("https://www.gnu.org/licenses/gpl-3.0.html") },
                        TermsOfService =
                            new Uri("https://www.termsfeed.com/terms-service/af183b5d3137b21df9b7da13585dc3ae"),
                        Title = "SimpleFiction API",
                        Version = "0.1.0"
                    });

                    options.IncludeXmlComments(xmlPath);
                });

            // Mvc
            services
                .AddMvc(options =>
                {
                    options.RespectBrowserAcceptHeader = true;
                    options.OutputFormatters.OfType<StringOutputFormatter>().Single().SupportedMediaTypes.Add("text/html");
                })
                .AddNewtonsoftJson(options => options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore)
                //.AddXmlSerializerFormatters()
                .SetCompatibilityVersion(CompatibilityVersion.Latest);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app
                    .UseDeveloperExceptionPage()
                    .UseMiddleware<ExceptionMiddleware>()
                    .UseBlazorDebugging();
            }
            else
            {
                app
                    .UseHsts()
                    .UseMiddleware<ExceptionMiddleware>()
                    .UseExceptionHandler();
            }

            app
                .UseResponseCompression()
                .UseHttpsRedirection()
                .UseSwagger()
                .UseClientSideBlazorFiles<Client.Startup>()
                .UseRouting()
                .UseEndpoints(endpoints =>
                {
                    endpoints.MapDefaultControllerRoute();
                    endpoints.MapFallbackToClientSideBlazor<Client.Startup>("index.html");
                })
                .UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/doc/swagger.json", "SimpleFiction API V0");
                    options.RoutePrefix = "api";
                    options.IndexStream = () =>
                        GetType().GetTypeInfo().Assembly
                            .GetManifestResourceStream("FictionScraper.Server.SwaggerCustom.swagger_custom_index.html");
                });
        }
    }
}
