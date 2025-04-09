using Kzone.Engine.Controller.Application.Configurations;
using System.Threading;
using System.Threading.Tasks;

namespace Kzone.Engine.Controller.Application.Interfaces
{
    public interface IEngineSetupService
    {
        Task<EngineLaunchConfig> InitializeNewSession(CancellationToken token);
        Task WaitUntilEngineStarted(CancellationToken token);
    }
}