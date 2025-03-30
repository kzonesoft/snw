using KzoneSyncService.Application.Interfaces.Services;
using KzoneSyncService.Infrastructure.Extensions;
using KzoneSyncService.Presentation.Web.Backend.Extensions;
using Nancy;

namespace KzoneSyncService.Presentation.Web.Backend.Modules
{
    public class LoginModule : NancyModule
    {
        public LoginModule(IServerSideSettingService serverSideSetting)
        {

            Get["/api/verify-token"] = _ => Context.IsAuthenticated() ? HttpStatusCode.OK : HttpStatusCode.Unauthorized;

            // API xử lý đăng nhập
            Post["/api/login"] = _ =>
            {
                string password = Request.Body.AsString();
                var guiCfg = serverSideSetting.GetViewControlConfig();
                // Kiểm tra mật khẩu
                if (password == guiCfg.ViewControlPassword)
                {
                    var token = JwtHelper.GenerateJwtToken();
                    return Response.AsJson(new { token, success = true, message = "Đăng nhập thành công" });
                }
                return Response.AsJson(new { success = false, message = "Mật khẩu không đúng" });
            };
        }
    }
}
