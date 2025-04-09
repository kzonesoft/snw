using Kzone.Engine.Controller.Domain.Entities;

namespace Kzone.Engine.Controller.Infrastructure.Api.Responses
{
    public abstract class BaseAddResponse : BaseResponse
    {
        public Torrent AddedTorrent { get; set; }
    }
}
