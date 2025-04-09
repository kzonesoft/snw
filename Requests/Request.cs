using Kzone.Engine.Controller.Infrastructure.Api.Responses;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;

namespace Kzone.Engine.Controller.Infrastructure.Api.Requests
{
    public class Request : BaseRequest<Response>
    {
        #region Properties

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly UrlActionCollection AuthorizedList = new UrlActionCollection(GenerateAuthorizedUrl());

        #endregion

        /// <summary>
        /// Tạo danh sách các URL actions được phép
        /// </summary>
        private static IEnumerable<UrlAction> GenerateAuthorizedUrl()
        {
            yield return UrlAction.Default;
            yield return UrlAction.Start;
            yield return UrlAction.Stop;
            yield return UrlAction.Pause;
            yield return UrlAction.ForceStart;
            yield return UrlAction.Unpause;
            yield return UrlAction.Recheck;
            yield return UrlAction.Remove;
            yield return UrlAction.RemoveData;
            yield return UrlAction.RemoveDataTorrent;
            yield return UrlAction.RemoveTorrent;
            yield return UrlAction.SetPriority;
            yield return UrlAction.AddUrl;
            yield return UrlAction.AddFile;
            yield return UrlAction.GetFiles;
            yield return UrlAction.GetSettings;
            yield return UrlAction.SetSetting;
            yield return UrlAction.GetProps;
            yield return UrlAction.SetProps;
        }

        protected override void ToUrl(StringBuilder sb)
        {
            // Không cần thêm các tham số trong trường hợp Request cơ bản
        }

        // Đây là phương thức đúng theo abstract trong BaseRequest
        protected override void OnProcessingRequest(HttpClient httpClient, HttpRequestMessage requestMessage)
        {
            // Không cần xử lý đặc biệt cho request cơ bản
        }

        protected override void OnProcessedRequest(Response result)
        {
            // Không cần xử lý đặc biệt sau khi nhận response
        }

        protected override bool CheckAction(UrlAction action)
        {
            return AuthorizedList.Any(a => a.Equals(action));
        }
    }
}