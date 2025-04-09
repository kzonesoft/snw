using System;

namespace Kzone.Engine.Controller.Domain.Exceptions
{
    public class EngineFrozenException : Exception
    {
        public EngineFrozenException(string mess) : base(mess)
        {

        }
    }
}
