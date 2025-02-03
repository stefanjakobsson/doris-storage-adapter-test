using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace DorisStorageAdapter.Server.Controllers.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class DevOnlyAttribute : Attribute, IFilterFactory
{
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        return new DevOnlyAttributeImpl(serviceProvider.GetRequiredService<IWebHostEnvironment>());
    }

    public bool IsReusable => true;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    private sealed class DevOnlyAttributeImpl(IWebHostEnvironment hostingEnv) : Attribute, IAuthorizationFilter
    {
        private IWebHostEnvironment HostingEnv { get; } = hostingEnv;

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (!HostingEnv.IsDevelopment())
            {
                context.Result = new NotFoundResult();
            }
        }
    }
}
