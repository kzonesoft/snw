using Kzone.Engine.Controller.Domain.Enums;
using System;
using System.Threading.Tasks;

namespace Kzone.Engine.Controller.Application.Interfaces
{
    public interface IEngineController
    {
        EngineStatus Status { get; }
        Action<string> Logger { set; }

        Task Start();
        void Stop();
    }
}