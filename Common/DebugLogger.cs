using System;

namespace Kzone.Signal
{
    public class DebugLogger
    {
        private Action<Severity, string> _logger = null;
        private Action<Exception> _exceptionRecord = null;

        public Action<Severity, string> Logger
        {
            get { return _logger; }
            set { _logger = value; }
        }

        public Action<Exception> ExceptionRecord
        {
            get { return _exceptionRecord; }
            set { _exceptionRecord = value; }
        }

    }
}
