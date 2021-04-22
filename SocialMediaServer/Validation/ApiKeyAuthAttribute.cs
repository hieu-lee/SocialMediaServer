using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Threading.Tasks;

namespace SocialMediaServer.Validation
{
    public class ApiKeyAuthAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.HttpContext.Request.Headers.TryGetValue("ApiKey", out var key))
            {
                context.Result = new UnauthorizedResult();
                return;
            }
            if (key!= "Jeg5dSHhmNd6GueSDbe754PPDUFn9SUbwhz4Zwjg")
            {
                context.Result = new UnauthorizedResult();
                return;
            }
            await next();
        }
    }
}
