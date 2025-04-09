using Kzone.Engine.Controller.Application.Configurations;
using Kzone.Engine.Controller.Application.Interfaces;
using Kzone.Engine.Controller.Domain.Exceptions;
using Kzone.Engine.Controller.Domain.Interfaces;
using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace Kzone.Engine.Controller.Application.Services
{
    public class EngineSetupService : IEngineSetupService
    {
        private readonly IEngineSettingsService _engineSettings;
        private readonly ITorrentOnDiskService _torrentOnDisk;
        private readonly ITorrentApi _torrentApi;

        private int _appInitTimeout = 15;
        public EngineSetupService(
            IEngineSettingsService engineSettings,
            ITorrentOnDiskService torrentOnDisk,
            ITorrentApi torrentApi)
        {
            _engineSettings = engineSettings ?? throw new ArgumentNullException(nameof(engineSettings));
            _torrentOnDisk = torrentOnDisk ?? throw new ArgumentNullException(nameof(torrentOnDisk));
            _torrentApi = torrentApi ?? throw new ArgumentNullException(nameof(torrentApi));
        }

        /// <summary>
        /// Khởi tạo phiên làm việc mới và trả về cấu hình khởi động
        /// </summary>
        public async Task<EngineLaunchConfig> InitializeNewSession(CancellationToken token)
        {
            var user = Guid.NewGuid().ToString("N");
            var port = await PortListenerGenerate(token).ConfigureAwait(false);
            var pass = "S3pvbmVTeW5jU2VydmljZUJ5QWRjRGV2LltodHRwczovL3d3dy5mYWNlYm9vay5jb20vYWRja3pvbmVd";

            // Thiết lập cấu hình
            await _engineSettings.SetupSession(port, user, pass, token).ConfigureAwait(false);

            var launchConfig = _engineSettings.GetLaunchConfig();
            _appInitTimeout = launchConfig.AppInitTimeout;
            return launchConfig;
        }

        public async Task WaitUntilEngineStarted(CancellationToken token)
        {
            await WaitApiInit(token);
            await WaitElementInit(token);
        }

        private async Task<int> PortListenerGenerate(CancellationToken token)
        {
            Random rng = new Random();
            while (true)
            {
                int port = rng.Next(49152, 65535);

                // Bỏ qua cổng cụ thể nếu cần
                if (port == 60888)
                {
                    await TaskEx.Delay(50, token).ConfigureAwait(false);
                    continue;
                }

                // Kiểm tra xem port có đang được dùng hay không
                if (!IsPortInUse(port))
                {
                    return port;
                }

                // Chờ một chút rồi thử lại
                await TaskEx.Delay(50, token).ConfigureAwait(false);
            }
        }

        private bool IsPortInUse(int port)
        {
            try
            {
                var ipGlobalProps = IPGlobalProperties.GetIPGlobalProperties();

                // Kiểm tra TCP Listeners
                var tcpListeners = ipGlobalProps.GetActiveTcpListeners();
                if (tcpListeners.Any(x => x.Port == port))
                    return true;

                // Kiểm tra TCP Connections (bao gồm ephemeral ports cho outbound)
                var tcpConnections = ipGlobalProps.GetActiveTcpConnections();
                if (tcpConnections.Any(x => x.LocalEndPoint.Port == port))
                    return true;

                // Kiểm tra UDP Listeners
                var udpListeners = ipGlobalProps.GetActiveUdpListeners();
                if (udpListeners.Any(x => x.Port == port))
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        private async Task WaitApiInit(CancellationToken token)
        {
            var beginTime = DateTime.Now;
            var timeOut = _appInitTimeout < 10 ? TimeSpan.FromMinutes(15) : TimeSpan.FromMinutes(_appInitTimeout);

            while (!token.IsCancellationRequested)
            {
                var isAlive = await _torrentApi.IsApiAlive(token).ConfigureAwait(false);
                if (isAlive)
                {
                    break;
                }

                //nếu khoảng thời gian thời điểm hiện tại và thời gian bắt đầu lớn hơn thời gian quy định => time out
                if (DateTime.Now - beginTime > timeOut)
                {
                    throw new EngineException("Engine API init timeout.");
                }

                await TaskEx.Delay(1000, token).ConfigureAwait(false);
            }
        }

        private async Task WaitElementInit(CancellationToken token)
        {
            var totalElement = _torrentOnDisk.TorrentCount();
            if (totalElement == 0) return;

            var beginTime = DateTime.Now;

            while (!token.IsCancellationRequested)
            {
                if (DateTime.Now - beginTime > TimeSpan.FromMinutes(5))
                {
                    throw new EngineException("Engine wait element timeout.");
                }

                var torrent = await _torrentApi.GetTorrents().ConfigureAwait(false);
#if DEBUG
                Console.WriteLine($"     *Total element count : {totalElement}{Environment.NewLine}     *Current torrent count : {torrent?.Count()}");
#endif
                if (torrent != null && torrent.Count() == totalElement)
                {
                    break;
                }
                await TaskEx.Delay(1000, token).ConfigureAwait(false);
            }
        }
    }
}