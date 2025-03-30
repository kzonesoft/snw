using Kzone.WksDto.Report;
using Nancy;
using Newtonsoft.Json;
using System.IO;

namespace KzoneSyncService.Presentation.Web.Backend.Modules
{
    public class GameAccessModule : NancyModule
    {
        public GameAccessModule()
        {
            Post["/api/gameaccess"] = parameters =>
            {
                // Đọc body từ request
                var requestBody = Request.Body;

                // Đọc nội dung JSON từ body
                string jsonString;
                using (var reader = new StreamReader(requestBody))
                {
                    jsonString = reader.ReadToEnd();
                }

                // Parse JSON thành một đối tượng C#
                var data = JsonConvert.DeserializeObject<GameAccessReportDto>(jsonString);

                // Xử lý logic dựa trên dữ liệu nhận được
                var responseMessage = $"Received wks: {data.WksName}, game: {data.GameName}";

                // Trả về phản hồi
                return Response.AsJson(new { message = responseMessage });
            };
        }
    }
}
