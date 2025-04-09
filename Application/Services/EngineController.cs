using Autofac;
using Kzone.Engine.Controller.Application.Interfaces;
using Kzone.Engine.Controller.Domain.Enums;
using Kzone.Engine.Controller.Infrastructure.CoreProcess;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kzone.Engine.Controller.Application.Services
{
    //singleton
    public class EngineController : IEngineController, IDisposable
    {
        private readonly ILifetimeScope _lifetimeScope;
        private readonly object _syncLock = new object();

        private ProcessSession _processSession;
        private CancellationTokenSource _cts;

        // Sử dụng biến số nguyên để theo dõi trạng thái thay vì các biến boolean
        private int _isStarting; // 0 = không đang khởi động, 1 = đang khởi động
        private int _isDisposed; // 0 = chưa dispose, 1 = đã dispose

        /// <summary>
        /// Khởi tạo EngineController
        /// </summary>
        /// <param name="lifetimeScope">Lifetime scope từ Autofac</param>
        public EngineController(ILifetimeScope lifetimeScope)
        {
            _lifetimeScope = lifetimeScope ?? throw new ArgumentNullException(nameof(lifetimeScope));
            _isStarting = 0;
            _isDisposed = 0;
        }

        /// <summary>
        /// Logger
        /// </summary>
        public Action<string> Logger { private get; set; }

        /// <summary>
        /// Trạng thái hiện tại của Engine
        /// </summary>
        public EngineStatus Status => GetCurrentStatus();

        /// <summary>
        /// Kiểm tra xem đối tượng đã bị dispose chưa
        /// </summary>
        private bool IsDisposed()
        {
            return Interlocked.CompareExchange(ref _isDisposed, 0, 0) == 1;
        }

        /// <summary>
        /// Khởi động Engine
        /// </summary>
        public async Task Start()
        {
            if (IsDisposed())
                throw new ObjectDisposedException(nameof(EngineController));

            var currentStatus = GetCurrentStatus();
            if (currentStatus == EngineStatus.Starting || currentStatus == EngineStatus.Started)
                throw new InvalidOperationException("Engine is already running.");

            // Cố gắng đặt trạng thái _isStarting từ 0 thành 1
            if (Interlocked.CompareExchange(ref _isStarting, 1, 0) != 0)
            {
                // Nếu đã có luồng khác đang khởi động, trả về lỗi
                throw new InvalidOperationException("Engine is already starting by another thread.");
            }

            CancellationTokenSource localCts = null;

            try
            {
                // Đảm bảo giải phóng tài nguyên cũ trước khi bắt đầu mới
                ReleaseResources();

                localCts = new CancellationTokenSource();

                // Thiết lập _cts trong khóa để tránh race condition
                lock (_syncLock)
                {
                    if (IsDisposed())
                        throw new ObjectDisposedException(nameof(EngineController));

                    _cts = localCts;
                }

                await StartEngineCore().ConfigureAwait(false);
            }
            catch
            {
                // Đảm bảo giải phóng tài nguyên nếu có lỗi
                ReleaseResources();
                throw;
            }
            finally
            {
                // Đặt lại trạng thái _isStarting về 0
                Interlocked.Exchange(ref _isStarting, 0);
            }
        }

        /// <summary>
        /// Khởi động Engine core
        /// </summary>
        private async Task StartEngineCore()
        {
            using (var scope = _lifetimeScope.BeginLifetimeScope())
            {
                var engineSetupService = scope.Resolve<IEngineSetupService>();
                ProcessSession oldSession = null;
                ProcessSession newSession = null;

                try
                {
                    CancellationToken token;
                    lock (_syncLock)
                    {
                        if (IsDisposed() || _cts == null)
                            throw new OperationCanceledException();
                        token = _cts.Token;
                    }

                    // Khởi tạo phiên mới
                    DebugLog("|_0_| Create new session...");
                    var launchConfig = await engineSetupService.InitializeNewSession(token);

                    // Lấy session cũ ra trước
                    lock (_syncLock)
                    {
                        if (IsDisposed() || _cts == null || _cts.IsCancellationRequested)
                            throw new OperationCanceledException();

                        oldSession = _processSession;
                        _processSession = null;
                    }

                    // Tạo process mới
                    DebugLog("|_1_| Create new process...");
                    using (var creator = new ProcessCreator(launchConfig.TokenCallback))
                    {
                        // Giải phóng session cũ (ở ngoài lock)
                        if (oldSession != null)
                        {
                            oldSession.Dispose();
                            oldSession = null;
                        }

                        // Tạo session mới
                        newSession = creator.Create(launchConfig.LicenseId);

                        // Kiểm tra trạng thái và gán session mới
                        lock (_syncLock)
                        {
                            if (IsDisposed() || _cts == null || _cts.IsCancellationRequested)
                            {
                                // Để giải phóng bên ngoài lock
                                throw new OperationCanceledException();
                            }

                            _processSession = newSession;
                            newSession = null; // Tránh giải phóng trong finally
                        }
                    }

                    // Chờ engine khởi động
                    DebugLog("|_2_| Engine starting...");
                    await engineSetupService.WaitUntilEngineStarted(token);
                    DebugLog("|_3_| Engine started!");

                    // Đảm bảo _processSession vẫn hợp lệ
                    ProcessSession currentSession;
                    lock (_syncLock)
                    {
                        if (IsDisposed() || _cts == null || _cts.IsCancellationRequested)
                            throw new OperationCanceledException();

                        currentSession = _processSession;
                    }

                    if (currentSession != null)
                    {
                        currentSession.StartDataMonitor();
                        DebugLog("|_4_| Data monitor started!");
                    }
                }
                catch (Exception)
                {
                    // Dọn dẹp session mới nếu bị lỗi
                    if (newSession != null)
                    {
                        newSession.Dispose();
                    }

                    // Đảm bảo null session nếu lỗi xảy ra trong quá trình khởi động
                    lock (_syncLock)
                    {
                        if (_processSession != null && oldSession == null) // Chỉ xóa nếu đã gán session mới
                        {
                            var temp = _processSession;
                            _processSession = null;
                            temp.Dispose();
                        }
                    }

                    throw;
                }
                finally
                {
                    // Đảm bảo giải phóng oldSession
                    if (oldSession != null)
                    {
                        oldSession.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Dừng Engine
        /// </summary>
        public void Stop()
        {
            ReleaseResources();
        }

        /// <summary>
        /// Giải phóng tài nguyên
        /// </summary>
        private void ReleaseResources()
        {
            ProcessSession oldSession = null;
            CancellationTokenSource oldCts = null;

            // Sử dụng lock với phạm vi nhỏ nhất có thể
            lock (_syncLock)
            {
                // Lưu tham chiếu cũ
                oldSession = _processSession;
                oldCts = _cts;

                // Reset biến
                _processSession = null;
                _cts = null;
            }

            // Xử lý giải phóng tài nguyên bên ngoài lock để tránh deadlock

            // Hủy CancellationTokenSource trước
            if (oldCts != null)
            {
                try
                {
                    oldCts.Cancel();
                    oldCts.Dispose();
                }
                catch (Exception ex)
                {
                    DebugLog($"Error disposing CTS: {ex.Message}");
                }
            }

            // Giải phóng ProcessSession
            if (oldSession != null)
            {
                try
                {
                    oldSession.Dispose();
                }
                catch (Exception ex)
                {
                    DebugLog($"Error disposing ProcessSession: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Lấy trạng thái hiện tại của Engine
        /// </summary>
        private EngineStatus GetCurrentStatus()
        {
            // Kiểm tra trạng thái Starting bằng Interlocked
            if (Interlocked.CompareExchange(ref _isStarting, 0, 0) == 1)
                return EngineStatus.Starting;

            ProcessSession session;
            lock (_syncLock)
            {
                session = _processSession;
            }

            if (session == null)
                return EngineStatus.Stopped;

            if (session.HasProcessExited())
                return EngineStatus.Crashed;

            return EngineStatus.Started;
        }

        /// <summary>
        /// Ghi log debug
        /// </summary>
        private void DebugLog(string message)
        {
            Logger?.Invoke(message);
        }

        /// <summary>
        /// Giải phóng tài nguyên
        /// </summary>
        public void Dispose()
        {
            // Sử dụng Interlocked để tránh dispose nhiều lần
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
                return; // Đã được dispose trước đó

            ReleaseResources();
            GC.SuppressFinalize(this);
        }

        ~EngineController()
        {
            Dispose();
        }
    }
}