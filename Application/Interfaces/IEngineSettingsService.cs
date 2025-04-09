using Kzone.Engine.Controller.Application.Configurations;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Kzone.Engine.Controller.Application.Interfaces
{
    public interface IEngineSettingsService
    {
        Task SetupSession(int port, string user, string password, CancellationToken token);
        Task SyncSettings();
        Task SetSettings(Dictionary<string, object> settings, bool applyToEngine = false);
        EngineLaunchConfig GetLaunchConfig();
    }
}