using Nancy;

namespace KzoneSyncService.Infrastructure.Networks.WebServer.Extensions
{
    public static class NancyContextExtension
    {
        public static void SetAuthenticated(this NancyContext context)
        {
            context.Items["IsAuth"] = true;
        }

        public static bool IsAuthenticated(this NancyContext context)
        {
            return context.Items.TryGetValue("IsAuth", out var isAuth) && isAuth is bool && (bool)isAuth;
        }
    }
}
