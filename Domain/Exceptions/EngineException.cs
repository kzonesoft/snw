using System;

namespace Kzone.Engine.Controller.Domain.Exceptions
{
    public class EngineException : Exception
    {
        public EngineException(string mess) : base(mess) { }
    }
}
