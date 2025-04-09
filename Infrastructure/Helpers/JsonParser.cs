using Kzone.Engine.Controller.Domain.Entities;
using Kzone.Engine.Controller.Domain.Enums;
using Kzone.Engine.Controller.Domain.Exceptions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kzone.Engine.Controller.Infrastructure.Helpers
{
    public static class JsonParser
    {
        public static Result ParseJsonResult(string json)
        {
            if (json == null)
                throw new ArgumentNullException(nameof(json));

            JObject o = JObject.Parse(json);
            return ParseJsonResult(o);
        }

        public static Result ParseJsonResult(JObject obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            Result result = new Result(obj);
            result.Build = ParseBase(obj, "build", j => j.Value<int>());
            result.Error = ParseBase(obj, "error", ParseError);
            result.CacheId = ParseBase(obj, "torrentc", j => j.Value<int>());
            result.Label.AddRangeIfNotNull(ParseBase(obj, "label", ParseLabels));
            result.Messages.AddRangeIfNotNull(ParseBase(obj, "messages", ParseMessages));
            result.Torrents.AddRangeIfNotNull(ParseBase(obj, "torrents", ParseTorrents));
            result.ChangedTorrents.AddRangeIfNotNull(ParseBase(obj, "torrentp", ParseTorrents));
            result.Files.AddRangeIfNotNull(ParseBase(obj, "files", ParseFiles));
            result.Settings.AddRangeIfNotNull(ParseBase(obj, "settings", ParseSettings));
            result.Props.AddRangeIfNotNull(ParseBase(obj, "props", ParseProps));

            return result;
        }

        private static T ParseBase<T>(JObject obj, string token, Func<JToken, T> parser)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            if (token == null)
                throw new ArgumentNullException(nameof(token));
            if (parser == null)
                throw new ArgumentNullException(nameof(parser));

            var jsonToken = obj.SelectToken(token, false);
            if (jsonToken == null)
                return default;

            return parser(jsonToken);
        }

        public static TorrentException ParseError(JToken obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            var error = obj.Value<string>();

            if (string.IsNullOrEmpty(error))
                return null;

            return new TorrentException(error);
        }

        public static IDictionary<string, FileCollection> ParseFiles(JToken obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            var result = new Dictionary<string, FileCollection>();

            for (int i = 0; i + 1 < obj.Count(); i += 2)
            {
                var oKey = obj[i].Value<string>();
                if (oKey == null)
                    continue;

                string hash = oKey.ToUpperInvariant();
                var files = new List<Files>();

                var oValue = obj[i + 1];
                if (oValue == null)
                    continue;

                foreach (var jfile in oValue)
                {
                    Files file = new Files();
                    file.Name = jfile[0]?.Value<string>();
                    file.Size = jfile[1]?.Value<long>() ?? 0;
                    file.Downloaded = jfile[2]?.Value<long>() ?? 0;
                    int priority = jfile[3]?.Value<int>() ?? 0;
                    if (priority <= 3 && priority >= 0)
                    {
                        file.Priority = (Priority)priority;
                    }
                    files.Add(file);
                }

                result.Add(hash, new FileCollection(files.OrderBy(f => f.Name)));
            }
            return result;
        }

        public static IList<Torrent> ParseTorrents(JToken obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            var result = new List<Torrent>();

            try
            {
                foreach (var t in obj)
                {
                    try
                    {
                        // Đảm bảo đủ phần tử để parse
                        if (t.Count() < 19)
                        {
                            continue; // Bỏ qua nếu không đủ dữ liệu
                        }

                        // Lấy các giá trị cơ bản, sử dụng default nếu parse thất bại
                        string hash = SafeGetValue(t, 0, "")?.ToUpperInvariant();
                        int statusValue = SafeGetValue(t, 1, 0);
                        string name = SafeGetValue(t, 2, "");
                        long size = SafeGetValue<long>(t, 3, 0);
                        int progress = SafeGetValue(t, 4, 0);
                        double uploadSpeed = SafeGetValue(t, 8, 0.0);
                        double downloadSpeed = SafeGetValue(t, 9, 0.0);
                        long remainingSize = SafeGetValue<long>(t, 18, 0);

                        // Tạo object Torrent mới với dữ liệu đã parse
                        var torrent = new Torrent
                        {
                            Hash = hash,
                            Name = name,
                            Size = size,
                            Progress = progress,
                            UploadSpeed = uploadSpeed,
                            DownloadSpeed = downloadSpeed,
                            RemainingSize = remainingSize,
                            Status = ParseStatusToString(statusValue, progress)
                        };

                        result.Add(torrent);
                    }
                    catch (Exception ex)
                    {
                        // Ghi log lỗi cho mỗi torrent và tiếp tục với torrent tiếp theo
                        Console.WriteLine($"Error parsing torrent: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                // Ghi log lỗi tổng quát
                Console.WriteLine($"Error parsing torrents collection: {ex.Message}");
                // Có thể throw lại exception hoặc trả về danh sách rỗng tùy theo chiến lược xử lý lỗi
            }

            return result;
        }

        // Helper method để lấy giá trị an toàn từ JToken array
        private static T SafeGetValue<T>(JToken array, int index, T defaultValue)
        {
            try
            {
                if (array == null || index >= array.Count())
                    return defaultValue;

                var value = array[index];
                if (value == null || value.Type == JTokenType.Null)
                    return defaultValue;

                return value.Value<T>();
            }
            catch
            {
                return defaultValue;
            }
        }

        private static string ParseStatusToString(int statusValue, int progress)
        {
            try
            {
                // Map các giá trị bitwise để kiểm tra trạng thái
                bool isStarted = (statusValue & 1) == 1;           // 1
                bool isChecking = (statusValue & 2) == 2;          // 2
                bool isStartAfterCheck = (statusValue & 4) == 4;   // 4
                bool isChecked = (statusValue & 8) == 8;           // 8
                bool isError = (statusValue & 16) == 16;           // 16
                bool isPaused = (statusValue & 32) == 32;          // 32
                bool isQueued = (statusValue & 64) == 64;          // 64
                bool isLoaded = (statusValue & 128) == 128;        // 128

                if (isError)
                    return "Error";

                if (isChecking)
                    return "Checking";

                if (progress >= 1000 && isStarted)
                    return "Success";

                if (isPaused)
                    return "Pause";

                if (isStarted)
                    return "Downloading";

                if (isLoaded && !(isStarted || isPaused || isQueued || isChecking))
                    return "Stop";

                if (isQueued)
                    return progress >= 1000 ? "Success" : "Wait";

                return "Wait";
            }
            catch
            {
                return "Unknown"; // Trả về trạng thái mặc định nếu có lỗi
            }
        }

        private static IList<string> ParseMessages(JToken obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            List<string> result = obj.Select(l => l.ToString()).ToList();
            return result;
        }

        private static IList<Props> ParseProps(JToken obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            var list = obj.Select(t => new Props
            {
                Hash = t["hash"].Value<string>().ToUpperInvariant(),
                Trackers = (t["trackers"].Value<string>() ?? string.Empty).Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries),
                UlRate = t["ulrate"].Value<int>(),
                DlRate = t["dlrate"].Value<int>(),
                Superseed = (PropsOption)t["superseed"].Value<int>(),
                DHT = (PropsOption)t["dht"].Value<int>(),
                PEX = (PropsOption)t["pex"].Value<int>(),
                SeedOverride = (PropsOption)t["seed_override"].Value<int>(),
                SeedRatio = t["seed_ratio"].Value<int>(),
                SeedTime = t["seed_time"].Value<int>(),
                UlSlots = t["ulslots"].Value<int>(),
            }).ToList();

            return list;
        }


        private static IList<Label> ParseLabels(JToken obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            List<Label> result = obj.Select(l => new Label { Count = l[1].Value<int>(), Name = l[0].Value<string>() }).ToList();
            return result;
        }



        public static IEnumerable<Setting> ParseSettings(JToken obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            foreach (var jToken in obj)
            {
                Setting setting = new Setting();
                setting.Key = jToken[0].Value<string>();
                setting.Type = (SettingType)jToken[1].Value<int>();

                string value = jToken[2].Value<string>();
                switch (setting.Type)
                {
                    case SettingType.Integer:
                        if (int.TryParse(value, out var i))
                        {
                            setting.Value = i;
                        }
                        else
                        {
                            setting.Value = value;
                        }
                        break;
                    case SettingType.Boolean:
                        setting.Value = value == "true";
                        break;
                    case SettingType.String:
                        setting.Value = value;
                        break;
                }

                if (jToken.Count() > 3)
                {
                    var accessJtoken = jToken[3].SelectToken("access");
                    setting.Access = accessJtoken.Value<string>();
                }

                yield return setting;
            }
        }
    }
}
