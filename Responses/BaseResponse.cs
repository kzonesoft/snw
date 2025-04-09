using Kzone.Engine.Controller.Domain.Entities;
using Kzone.Engine.Controller.Domain.Exceptions;

namespace Kzone.Engine.Controller.Infrastructure.Api.Responses
{
    public abstract class BaseResponse
    {
        private Result _result;

        /// <summary>
        /// Request result
        /// </summary>
        public Result Result
        {
            get { return _result; }
            set
            {
                if (_result == value)
                    return;

                _result = value;
                OnResultChange();
            }
        }

        /// <summary>
        /// µTorrent result error
        /// </summary>
        public TorrentException Error => _result?.Error;

        protected abstract void OnResultChange();
    }
}
