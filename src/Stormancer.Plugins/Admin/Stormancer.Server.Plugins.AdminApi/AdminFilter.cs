using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.WebApi
{
    /// <summary>
    /// Type of web Api.
    /// </summary>
    public enum WebApiType
    {
        /// <summary>
        /// Api exposed on admin endpoints.
        /// </summary>
        Admin = 1,

        /// <summary>
        /// Api exposed on public endpoints.
        /// </summary>
        Public = 2,

        /// <summary>
        /// Api exposed on both endpoints.
        /// </summary>
        Both = 3
    }
    /// <summary>
    /// Marks a controller or action as admin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class WebApiTypeAttribute : ActionFilterAttribute
    {
        /// <summary>
        /// Creates a new <see cref="WebApiTypeAttribute"/> object.
        /// </summary>
        /// <param name="type"></param>
        public WebApiTypeAttribute(WebApiType type)
        {
            Type = type;
        }

        /// <summary>
        /// Gets the type of the web Api.
        /// </summary>
        public WebApiType Type { get; }
    }



    internal class AppWebApiFeature
    {
        public bool IsAdminEndpoint { get; set; }
    }

    internal class AppWebApiMiddleware
    {
        private readonly RequestDelegate _next;

        public AppWebApiMiddleware(RequestDelegate next, bool isAdmin)
        {
            _next = next;
            IsAdmin = isAdmin;
        }

        public bool IsAdmin { get; }

        public Task OnRequestAsync(HttpContext context)
        {
            context.Features.Set(new AppWebApiFeature { IsAdminEndpoint = IsAdmin });

            return _next(context);
        }

        public static RequestDelegate AsPublicApi(RequestDelegate next)
        {
            var middleware = new AppWebApiMiddleware(next, false);
            return middleware.OnRequestAsync;
        }

        public static RequestDelegate AsAdminApi(RequestDelegate next)
        {
            var middleware = new AppWebApiMiddleware(next, true);

            return middleware.OnRequestAsync;
        }
    }

    internal class WebApiTypeFilter : IAsyncActionFilter
    {
        public WebApiTypeFilter()
        {

        }

        private WebApiType GetWebApiType(ActionDescriptor actionDescriptor)
        {
            if (actionDescriptor is ControllerActionDescriptor descriptor)
            {
                var attr = descriptor.MethodInfo.GetCustomAttribute<WebApiTypeAttribute>(true);
                if (attr == null)
                {
                    attr = descriptor.ControllerTypeInfo.GetCustomAttribute<WebApiTypeAttribute>(true);
                }
                return attr?.Type ?? WebApiType.Admin;
               
            }
            return WebApiType.Admin;
        }


        public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var feature = context.HttpContext.Features.Get<AppWebApiFeature>();
            Debug.Assert(feature != null);

            var apiType = GetWebApiType(context.ActionDescriptor);

            if ((apiType & WebApiType.Admin) == 0 && feature.IsAdminEndpoint)
            {
                context.Result = new NotFoundResult();
                return Task.CompletedTask;
            }
            if ((apiType & WebApiType.Public) == 0 && !feature.IsAdminEndpoint)
            {
                context.Result = new NotFoundResult();
                return Task.CompletedTask;
            }
            return next();
        }
    }

    internal class WebApiDocumentFilter : IDocumentFilter
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public WebApiDocumentFilter(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            var httpContext = _httpContextAccessor.HttpContext;

            Debug.Assert(httpContext != null);
            var request = httpContext.Request;
            var feature = httpContext.Features.Get<AppWebApiFeature>();
            Debug.Assert(feature != null);

            foreach (var description in context.ApiDescriptions)
            {
                // get the type of the api, default to admin.
                var apiType = description.CustomAttributes().OfType<WebApiTypeAttribute>().FirstOrDefault()?.Type ?? WebApiType.Admin;

                if ((apiType & WebApiType.Admin) == 0 && feature.IsAdminEndpoint)
                {
                    //remove non admin apis from admin endpoint.

                    if (description.RelativePath != null && description.HttpMethod !=null)
                    {
                        var paths = swaggerDoc.Paths["/" + description.RelativePath];
                        var operationType = Enum.Parse<OperationType>(description.HttpMethod, true);
                        paths.Operations.Remove(operationType);
                        if (paths.Operations.Count == 0)
                        {

                            swaggerDoc.Paths.Remove(description.RelativePath);
                        }
                    }
                    
                }
                if ((apiType & WebApiType.Public) == 0 && !feature.IsAdminEndpoint)
                {
                    if (description.RelativePath != null && description.HttpMethod != null)
                    {
                        //remove non public apis from public endpoint.
                        var paths = swaggerDoc.Paths["/" + description.RelativePath];
                        var operationType = Enum.Parse<OperationType>(description.HttpMethod, true);
                        paths.Operations.Remove(operationType);
                        if (paths.Operations.Count == 0)
                        {
                            swaggerDoc.Paths.Remove(description.RelativePath);
                        }
                    }

                }
            }
        }
    }



}
