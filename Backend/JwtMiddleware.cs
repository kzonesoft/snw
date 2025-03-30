using KzoneSyncService.Presentation.Web.Backend.Extensions;
using Nancy.Bootstrapper;
using System.Linq;

namespace KzoneSyncService.Presentation.Web.Backend
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
