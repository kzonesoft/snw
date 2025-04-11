using KzoneSyncService.Infrastructure.Networks.WebServer.Extensions;
using Nancy.Bootstrapper;
using System.Linq;

namespace KzoneSyncService.Infrastructure.Networks.WebServer
{
    public class JwtMiddleware
    {
        public static void Register(IPipelines pipelines)
        {
            pipelines.BeforeRequest.AddItemToStartOfPipeline(ctx =>
            {
                var token = ctx.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");

                if (!string.IsNullOrEmpty(token))
                {
                    if (JwtHelper.ValidateJwtToken(token))
                        ctx.SetAuthenticated();
                }
                return null;
            });
        }
    }
}
