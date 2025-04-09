using Kzone.Engine.Controller.Application.Extensions;
using Kzone.Engine.Controller.Domain.Exceptions;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Kzone.Engine.Controller.Infrastructure.CoreProcess
{
    public class ProcessCreator : IDisposable
    {
        #region P/Invoke Definitions

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate string StringCallback(string input);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetRdcTokenCallbackDelegate(StringCallback callback);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int AnDapChaiEngineDelegate(string uid, string key);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int CleanupDelegate();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern SafeLibraryHandle LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(SafeLibraryHandle hModule, string procName);

#if DEBUG
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;
        private const int SW_SHOWNORMAL = 1;
#endif

        #endregion

        #region Private Properties

        // Khóa để đảm bảo thread-safety khi dispose
        private readonly object _disposeLock = new object();
        private readonly StringCallback _tokenCallBack;
        private SafeLibraryHandle _dllHandle;
        private SetRdcTokenCallbackDelegate _setRdcTokenCallback;
        private AnDapChaiEngineDelegate _anDapChaiEngine;
        private CleanupDelegate _cleanup;
        private bool _disposed;

        // Mutex toàn cục để đảm bảo chỉ có một instance đang kiểm tra và kết thúc processes
        private static readonly Mutex _processManagementMutex = new Mutex(false, "KzoneProcessManagementMutex");

        // Thời gian timeout cho việc acquire mutex
        private const int MUTEX_TIMEOUT_MS = 5000;

        #endregion

        /// <summary>
        /// Khởi tạo ProcessCreator với callback để xử lý token.
        /// </summary>
        /// <param name="tokenCallBack">Callback function để xử lý token</param>
        /// <param name="dllPath">Đường dẫn đến DLL, mặc định là "Kzone.Engine.Core.dll"</param>
        public ProcessCreator(Func<string, string> tokenCallBack, string dllPath = "Kzone.Engine.Core.dll")
        {
            if (tokenCallBack == null)
                throw new ArgumentNullException(nameof(tokenCallBack));

            try
            {
                _dllHandle = LoadLibrary(dllPath);
                if (_dllHandle.IsInvalid)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new EngineException($"Failed to load {dllPath}. Error code: {errorCode}");
                }

                _setRdcTokenCallback = LoadFunction<SetRdcTokenCallbackDelegate>("set_rdc_token_callback");
                _anDapChaiEngine = LoadFunction<AnDapChaiEngineDelegate>("an_dap_chai_engine");
                _cleanup = LoadFunction<CleanupDelegate>("freeup_memory_resource");

                _tokenCallBack = new StringCallback(tokenCallBack); // Lưu delegate
                _setRdcTokenCallback(_tokenCallBack); // Truyền delegate cho callback
            }
            catch (Exception ex)
            {
                // Giải phóng tài nguyên nếu có lỗi trong quá trình khởi tạo
                UnloadImportedDll();
                throw new EngineException($"Error initializing ProcessCreator: {ex.Message}");
            }
        }

        /// <summary>
        /// Tạo ProcessManager và khởi động process engine.
        /// </summary>
        /// <param name="licenseId">ID license để khởi động engine</param>
        /// <param name="secretKey">Secret key để khởi động engine</param>
        /// <returns>ProcessManager quản lý process đã được khởi động</returns>
        public ProcessSession Create(string licenseId, string secretKey = null)
        {
            if (string.IsNullOrEmpty(licenseId))
                throw new ArgumentNullException(nameof(licenseId), "License ID cannot be null or empty.");

            // Sử dụng secret key từ tham số hoặc sử dụng giá trị mặc định
            secretKey = secretKey ?? "oXFQjFjo6Ea4H557hN7A71vNkXNlraVA";

            lock (_disposeLock)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(ProcessCreator));

                // Tạo ProcessManager mới
                ProcessSession processManager = new ProcessSession();

                try
                {
                    // Kiểm tra và kết thúc các engine process đang chạy khác
                    CheckAnotherEngineProcessRunning();

                    // Khởi động engine process với số lần thử lại
                    int maxRetries = 3;
                    int retryDelayMs = 500;
                    Exception lastException = null;

                    for (int retry = 0; retry < maxRetries; retry++)
                    {
                        try
                        {
                            var resultCode = _anDapChaiEngine(licenseId, secretKey);
                            if (resultCode < 1)
                            {
                                string errorMessage = GetErrorMessage(resultCode);
                                throw new EngineException(errorMessage);
                            }

                            // Lấy process từ process ID
                            if (!ProcessExtension.GetProcessById(resultCode, out Process process))
                            {
                                throw new EngineException("The process has exited or does not exist");
                            }

                            // Thiết lập process cho ProcessManager
                            processManager.SetProcess(process);

#if DEBUG
                            // Ẩn cửa sổ process trong chế độ debug
                            IntPtr mainWindowHandle = process.MainWindowHandle;
                            if (mainWindowHandle != IntPtr.Zero)
                            {
                                ShowWindow(mainWindowHandle, SW_HIDE);
                            }
#endif

                            return processManager;
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;

                            // Nếu không phải là lần thử cuối, thì đợi một chút trước khi thử lại
                            if (retry < maxRetries - 1)
                            {
                                Thread.Sleep(retryDelayMs * (retry + 1)); // Tăng thời gian đợi theo số lần thử
                                retryDelayMs *= 2; // Exponential backoff
                            }
                        }
                    }

                    // Nếu tất cả các lần thử đều thất bại
                    throw new EngineException($"Failed to create process after {maxRetries} attempts");
                }
                catch (Exception)
                {
                    // Đảm bảo giải phóng processManager nếu có lỗi
                    processManager.Dispose();
                    throw;
                }
            }
        }

        /// <summary>
        /// Chuyển đổi mã lỗi thành thông báo lỗi
        /// </summary>
        private string GetErrorMessage(int errorCode)
        {
            return errorCode switch
            {
                -23 => "First input invalid.",
                -24 => "Create request id not working.",
                -25 => "RDC token invalid.",
                -26 => "Model parse failure",
                -27 => "RDC authenticate failure (data not match)",
                -28 => "Local resource invalid",
                -29 => "Engine initialization failed, unknown error.",
                -30 => "Secret key not match",
                -31 => "Create process unsuccessful with attempt 15 times",
                -32 => "Allocation memory failure",
                _ => $"Unknown error on initialization process: {errorCode}",
            };
        }

        /// <summary>
        /// Kiểm tra và đóng các engine process đang chạy từ tiến trình khác
        /// Sử dụng mutex để đảm bảo chỉ có một instance thực hiện việc này
        /// </summary>
        private void CheckAnotherEngineProcessRunning()
        {
            bool acquiredMutex = false;
            try
            {
                // Cố gắng acquire mutex với timeout
                acquiredMutex = _processManagementMutex.WaitOne(MUTEX_TIMEOUT_MS);
                if (!acquiredMutex)
                {
                    // Nếu không thể acquire mutex, có thể có instance khác đang xử lý, log và tiếp tục
                    System.Diagnostics.Debug.WriteLine("Could not acquire process management mutex. Another instance may be managing processes.");
                    return;
                }

                // Lấy ID của process hiện tại
                var currentProcessId = Process.GetCurrentProcess().Id;

                // Tìm tất cả các processes "KzoneSyncService"
                var kzoneProcessList = Process.GetProcessesByName("KzoneSyncService").ToArray();
                if (kzoneProcessList == null || !kzoneProcessList.Any()) return;

                // Lấy thông tin về parent process ID (nếu có thể)
                int? parentProcessId = null;
                // Mã để lấy parent process ID nếu cần

                foreach (var process in kzoneProcessList)
                {
                    try
                    {
                        // Bỏ qua process hiện tại
                        if (process.Id == currentProcessId)
                            continue;

                        // Bỏ qua parent process nếu có
                        if (parentProcessId.HasValue && process.Id == parentProcessId.Value)
                            continue;

                        // Kết thúc process với timeout
                        process.Terminator(30000); // Giảm timeout xuống 30 giây
                    }
                    catch (Exception ex)
                    {
                        // Ghi log lỗi nhưng không throw exception
                        System.Diagnostics.Debug.WriteLine($"Error terminating process {process.Id}: {ex.Message}");
                    }
                }
            }
            finally
            {
                // Đảm bảo luôn release mutex nếu đã acquire
                if (acquiredMutex)
                {
                    _processManagementMutex.ReleaseMutex();
                }
            }
        }

        /// <summary>
        /// Load function từ DLL
        /// </summary>
        private T LoadFunction<T>(string functionName) where T : class
        {
            lock (_disposeLock)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(ProcessCreator));

                if (_dllHandle == null || _dllHandle.IsInvalid)
                    throw new EngineException("DLL handle is invalid.");

                IntPtr funcPtr = GetProcAddress(_dllHandle, functionName);
                if (funcPtr == IntPtr.Zero)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new EngineException($"Failed to get function pointer for {functionName}. Error code: {errorCode}");
                }

                return Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(T)) as T;
            }
        }

        /// <summary>
        /// Giải phóng DLL đã load
        /// </summary>
        private void UnloadImportedDll()
        {
            if (_dllHandle != null && !_dllHandle.IsInvalid)
            {
                _dllHandle.Dispose();
                _dllHandle = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            lock (_disposeLock)
            {
                if (_disposed)
                    return;

                if (disposing)
                {
                    try
                    {
                        // Thứ tự giải phóng resource quan trọng:
                        // 1. Hủy đăng ký callback
                        if (_setRdcTokenCallback != null)
                        {
                            _setRdcTokenCallback(null);
                            _setRdcTokenCallback = null;
                        }

                        // 2. Gọi cleanup từ DLL
                        try
                        {
                            _cleanup?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error during cleanup: {ex.Message}");
                        }
                        _cleanup = null;

                        // 3. Giải phóng các delegate khác
                        _anDapChaiEngine = null;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error during Dispose: {ex.Message}");
                    }
                    finally
                    {
                        // 4. Giải phóng DLL handle
                        UnloadImportedDll();
                    }
                }

                _disposed = true;
            }
        }

        ~ProcessCreator()
        {
            Dispose(false);
        }
    }
}