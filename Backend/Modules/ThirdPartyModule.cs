using Nancy;

namespace KzoneSyncService.Presentation.Web.Backend.Modules
{
    public class ThirdPartyModule : NancyModule
    {
        public ThirdPartyModule()
        {
            Get["/api/thirdparty/license"] = parameters =>
            {
                return Response.AsJson(new { success = false, message = "..." });
            };

        }
    }
}
