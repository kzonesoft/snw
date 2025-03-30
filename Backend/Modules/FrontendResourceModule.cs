using KzoneSyncService.Application.Interfaces.AppSessions;
using KzoneSyncService.Presentation.Web.Backend.Extensions;
using Nancy;
using System;
using System.IO;
using System.Text;

namespace KzoneSyncService.Presentation.Web.Backend.Modules
{
    public class FrontendResourceModule : NancyModule
    {
        private ISessionStorage _sessionStorage;
        public FrontendResourceModule(ISessionStorage sessionStorage)
        {
            _sessionStorage = sessionStorage;

            // Route cho Pages

            Get["/"] = parameters => ServeAsset("login.html", "pages/");

            Get["/login"] = parameters => ServeAsset("login.html", "pages/");

            Get["/index"] = parameters => ServeAsset("index.html", "pages/");

            Get["/wks"] = _ =>
            {
                if (!Context.IsAuthenticated()) return HttpStatusCode.Unauthorized;
                return ServeAsset("wks.html", "pages/");
            };

            Get["/pages/{file*}"] = parameters => ServeAsset(parameters.file, "pages/");

            // Route cho CSS
            Get["/assets/css/{file*}"] = parameters => ServeAsset(parameters.file, "assets/css/");

            // Route cho Images
            Get["/assets/img/{file*}"] = parameters => ServeAsset(parameters.file, "assets/img/");

            // Route cho JavaScript
            Get["/assets/js/{file*}"] = parameters => ServeAsset(parameters.file, "assets/js/");



        }

        /// <summary>
        /// Hàm xử lý tải tài nguyên từ assembly.
        /// </summary>
        /// <param name="fileName">Tên tệp cần tải.</param>
        /// <param name="folder">Thư mục con trong "Assets".</param>
        /// <returns>Trả về nội dung tài nguyên hoặc HTTP 404 nếu không tìm thấy.</returns>
        private Response ServeAsset(string fileName, string prefix)
        {
            string resourceKey = prefix + fileName;
            // Kiểm tra xem file có tồn tại trong WebPackage không
            if (_sessionStorage.WebResourceStore.TryGetValue(resourceKey, out byte[] fileContent))
            {
                //Console.WriteLine($"Serving asset from WebPackage: {resourceKey} ({fileContent.Length} bytes)");
                // Lấy MIME type
                string mimeType = GetMimeType(fileName);

                // Đối với HTML, xử lý đặc biệt
                if (mimeType == "text/html")
                {
                    // Chuyển đổi byte[] thành chuỗi, giả định mã hóa UTF-8
                    string htmlContent = Encoding.UTF8.GetString(fileContent);
                    // Trả về phản hồi dạng text với Content-Type là text/html
                    return Response.AsText(htmlContent)
                        .WithContentType("text/html; charset=utf-8")
                        .WithStatusCode(HttpStatusCode.OK);
                }

                // Đối với các loại tệp khác, tiếp tục sử dụng stream
                return Response.FromStream(new MemoryStream(fileContent), mimeType)
                    .WithStatusCode(HttpStatusCode.OK);
            }
            Console.WriteLine($"Asset not found in WebPackage: {resourceKey}");
            return HttpStatusCode.NotFound;
        }


        private string GetMimeType(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLower();
            return extension switch
            {
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".ico" => "image/x-icon",
                ".html" => "text/html",  // Thêm MIME type cho HTML
                ".htm" => "text/html",   // Thêm MIME type cho HTM
                _ => "application/octet-stream",
            };
        }

    }
}
