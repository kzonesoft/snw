using Kzone.Engine.Controller.Infrastructure.Helpers;

namespace Kzone.Engine.Controller.Infrastructure.Api.Responses
{
    public class AddByteArrayResponse : BaseAddResponse
    {
        public TorrentInfo AddedTorrentInfo { get; set; }

        protected override void OnResultChange()
        {
        }
    }
}
