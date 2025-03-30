using KzoneSyncService.Application.Interfaces.AppSessions;
using KzoneSyncService.Presentation.Web.Backend.Extensions;
using Nancy;
using System.Linq;


namespace KzoneSyncService.Presentation.Web.Backend.Modules
{
    public class WksModule : NancyModule
    {
        private readonly ISessionStorage _sessionStorage;
        public WksModule(ISessionStorage sessionStorage)
        {
            _sessionStorage = sessionStorage;


            Get["/api/wks/hwusage"] = _ =>
            {
                if (!Context.IsAuthenticated()) return HttpStatusCode.Unauthorized;
                return Response.AsJson(_sessionStorage.SignalWksStore.GetAll().Select(info => new
                {
                    info.WksName,
                    info.CpuLoad,
                    info.CpuTemp,
                    info.CpuClock,
                    info.CpuPow,
                    info.CpuFan,
                    info.GpuLoad,
                    info.GpuTemp,
                    info.GpuClock,
                    info.GpuPow,
                    info.GpuFan,
                    info.RamUsage,
                    info.RamSpeed,
                    info.UploadSpeed,
                    info.DownloadSpeed,
                    info.LanSpeed,
                    info.Ping,
                    info.AppRunning,
                    info.Uptime
                }).OrderBy(x => x.WksName));
            };
        }


    }
}
