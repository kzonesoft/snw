using KzoneSyncService.Application.Interfaces.Services;
using KzoneSyncService.Infrastructure.Extensions;
using KzoneSyncService.Infrastructure.Networks.WebServer.Extensions;
using Nancy;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;


namespace KzoneSyncService.Presentation.WebApi
{
    public class ServerManagerModule : NancyModule
    {
        private readonly IServerHwInfoService _hwInfoService;
        public ServerManagerModule(IServerHwInfoService serverHwInfo, IServerSideSettingService serverSideSetting)
        {
            _hwInfoService = serverHwInfo;

            Get["/api/server/statistic"] = parameters =>
            {
                if (!Context.IsAuthenticated()) return HttpStatusCode.Unauthorized;
                var serverDto = _hwInfoService.GetServerHwInfo();
                return Response.AsJson(serverDto);
            };

            Post["/api/server/power"] = _ =>
            {
                if (!Context.IsAuthenticated()) return HttpStatusCode.Unauthorized;
                var baseBody = Request.Body.AsString();
                var powerAction = JsonConvert.DeserializeObject<PowerAction>(baseBody);
                if (powerAction == null || string.IsNullOrEmpty(powerAction.Action) || string.IsNullOrEmpty(powerAction.Password))
                {
                    return Response.AsJson(new { success = false, message = "Request không hợp lệ" });
                }
                var viewConfig = serverSideSetting.GetViewControlConfig();
                // Kiểm tra mật khẩu
                if (!string.IsNullOrEmpty(viewConfig.PowerActionPassword) && viewConfig.PowerActionPassword == powerAction.Password )
                {

                    Console.WriteLine(powerAction.Action);
                    return Response.AsJson(new { success = true, message = $"Đã thực hiện {powerAction.Action}" });
                }
                return Response.AsJson(new { success = false, message = "Mật khẩu không đúng" });
            };
        }

        private bool ExecutePowerAction(string action)
        {
            try
            {
                switch (action.ToLower())
                {
                    case "shutdown":
                        // Sử dụng Process để thực thi lệnh shutdown
                        System.Diagnostics.Process.Start("shutdown", "/s /t 10 /c \"Server shutdown requested from web interface\"");
                        return true;
                    case "restart":
                        // Sử dụng Process để thực thi lệnh restart
                        System.Diagnostics.Process.Start("shutdown", "/r /t 10 /c \"Server restart requested from web interface\"");
                        return true;
                    default:
                        Console.WriteLine($"Unknown power action: {action}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing power action: {ex.Message}");
                return false;
            }
        }


        private class PowerAction
        {
            public string Action { get; set; } //restart, shutdown
            public string Password { get; set; }
        }



    }
}
