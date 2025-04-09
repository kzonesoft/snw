using Kzone.Engine.Bencode.Interfaces;
using Kzone.Engine.Controller.Application.Configurations;
using Kzone.Engine.Controller.Application.Interfaces;
using Kzone.Engine.Controller.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Kzone.Engine.Controller.Application.Services
{
    public class EngineSettingsService : IEngineSettingsService
    {
        private readonly IEngineSettingRepository _settingRepository;
        private readonly IEngineConfigProvider _configProvider;
        private readonly ITorrentApi _torrentApi;

        public EngineSettingsService(IEngineSettingRepository settingRepository, IEngineConfigProvider configProvider, ITorrentApi torrentApi)
        {
            _settingRepository = settingRepository ?? throw new ArgumentNullException(nameof(settingRepository));
            _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
            _torrentApi = torrentApi ?? throw new ArgumentNullException(nameof(torrentApi));
        }

        public EngineLaunchConfig GetLaunchConfig()
        {
            return _configProvider.GetLaunchConfig();
        }

        public async Task SetupSession(int port, string user, string password, CancellationToken token)
        {
            var engineConfig = _configProvider.GetRuntimeConfig();
            var engineSettings = CreateEngineSettings(engineConfig);

            // Thêm cài đặt API
            engineSettings.Add("webui.username", user);
            engineSettings.Add("webui.port", port);
            engineSettings.Add("webui.password", password);

            // Lưu cài đặt vào repository
            await UpdateRepositorySettingsAsync(engineSettings).ConfigureAwait(false);

            // Cấu hình kết nối API
            _torrentApi.ConfigureAccess(port, user, password);
        }

        public async Task SyncSettings()
        {
            var engineConfig = _configProvider.GetRuntimeConfig();
            var settings = CreateEngineSettings(engineConfig);

            await _torrentApi.SetSettings(settings).ConfigureAwait(false);
        }


        public async Task SetSettings(Dictionary<string, object> settings, bool applyToEngine = false)
        {
            // Lưu cài đặt vào repository
            await UpdateRepositorySettingsAsync(settings).ConfigureAwait(false);

            // Áp dụng cài đặt vào engine nếu được yêu cầu
            if (applyToEngine)
            {
                await _torrentApi.SetSettings(settings).ConfigureAwait(false);
            }
        }

        #region Private Methods

        private Dictionary<string, object> CreateEngineSettings(EngineRuntimeConfig config)
        {
            return new Dictionary<string, object>
            {
                { "max_ul_rate", config.UploadLimited },
                { "max_dl_rate", config.DownloadLimited },
                { "max_active_downloads", config.DownloadActiveLimit },
                { "bind_port", config.EngineListenPort },
                { "cache.override_size", config.DiskCache }
            };
        }

        private async Task UpdateRepositorySettingsAsync(Dictionary<string, object> settings)
        {
            // Áp dụng các cài đặt mới
            foreach (var setting in settings)
            {
                _settingRepository.AddOrUpdate(setting.Key, setting.Value);
            }

            // Xóa các cài đặt đường dẫn không cần thiết
            _settingRepository.Remove("dir_active_download");
            _settingRepository.Remove("dir_torrent_files");

            // Lưu thay đổi
            await _settingRepository.SaveChangesAsync().ConfigureAwait(false);
        }

        #endregion
    }
}