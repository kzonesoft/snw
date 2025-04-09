using Kzone.Engine.Controller.Application.Configurations;

namespace Kzone.Engine.Controller.Application.Interfaces
{
    public interface IEngineConfigProvider
    {
        EngineLaunchConfig GetLaunchConfig();
        EngineRuntimeConfig GetRuntimeConfig();
    }
}
