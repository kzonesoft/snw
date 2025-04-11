using Kzone.WksDto.Report;
using KzoneSyncService.Application.Interfaces.AppSessions;
using KzoneSyncService.Application.Interfaces.Services;
using KzoneSyncService.Infrastructure.Networks.WebServer.Extensions;
using Nancy;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;

namespace KzoneSyncService.Presentation.WebApi
{
    public class GamesModule : NancyModule
    {
        private readonly Lazy<ISessionStorage> _sessionStorage;
        private readonly Lazy<ITorrentPackageService> _torrentPackage;
        public GamesModule(Lazy<ISessionStorage> sessionStorage,Lazy<ITorrentPackageService> torrentPackage)
        {
            _sessionStorage = sessionStorage;
            _torrentPackage = torrentPackage;

            Post["/api/games/access"] = parameters =>
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


            Get["/api/games/downloaded"] = parameters =>
            {
                if (!Context.IsAuthenticated()) return HttpStatusCode.Unauthorized;
                return Response.AsJson(GetGameDownloaded());
            };

            Get["/api/games/downloading"] = parameters =>
            {
                if (!Context.IsAuthenticated()) return HttpStatusCode.Unauthorized;
                return Response.AsJson(GetGameDownloading());
            };
        }

        private object GetGameDownloaded()
        {
            try
            {
                var gameDownloadeds = _torrentPackage.Value.GetMenuPacks().OrderBy(x => x.Name);
                return gameDownloadeds;
            }
            catch (Exception)
            {

                return null;
            }
          
        }

        private object GetGameDownloading()
        {
            try
            {
                var dataStore = _sessionStorage.Value.EngineDataStore;
                return dataStore.GetAll();
            }
            catch (Exception)
            {
                return null;
            }
           
        }
    }
}
