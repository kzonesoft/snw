using Kzone.Engine.Controller.Infrastructure.Helpers;

namespace Kzone.Engine.Controller.Infrastructure.Api.Responses
{
    public class AddStreamResponse : BaseAddResponse
    {
        public TorrentInfo AddedTorrentInfo { get; set; }

        protected override void OnResultChange()
        {
        }
    }
}
