using Kzone.Engine.Controller.Application.Extensions;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Kzone.Engine.Controller.Infrastructure.CoreProcess
{
    public class ProcessSession : IDisposable
    {
        // Sử dụng biến nguyên tử thay cho biến boolean
        private int _disposed;
        // Sử dụng lock tối thiểu và chỉ cho các tài nguyên chia sẻ quan trọng
        private readonly object _processLock = new object();

        private readonly SafeFileHandle _jobHandle;
        private readonly Stopwatch _processLifetime;
        private Process _process;
        private int _processId;

        #region P/Invoke Definitions
        // Giữ nguyên các định nghĩa P/Invoke
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeFileHandle CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(SafeFileHandle hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(SafeFileHandle hJob, JobObjectInfoClass infoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryInformationJobObject(SafeFileHandle hJob, JobObjectInfoClass infoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength, IntPtr lpReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        private const uint STILL_ACTIVE = 259;


        private enum JobObjectInfoClass
        {
            JobObjectBasicLimitInformation = 2,
            JobObjectBasicUIRestrictions = 4,
            JobObjectEndOfJobTimeInformation = 6,
            JobObjectExtendedLimitInformation = 9
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_END_OF_JOB_TIME_INFORMATION
        {
            public uint EndOfJobTimeAction;
        }

        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
        private const uint JOB_OBJECT_LIMIT_JOB_TIME = 0x4;
        #endregion

        public ProcessSession()
        {
            _processLifetime = new Stopwatch();
            _processId = -1;
            _disposed = 0; // 0 = false, 1 = true

            // Tạo job object với tên duy nhất
            string jobName = $"{nameof(ProcessSession)}_{Process.GetCurrentProcess().Id}_{DateTime.Now.Ticks}";
            _jobHandle = CreateJobObject(IntPtr.Zero, jobName);

            if (_jobHandle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                // Thiết lập cấu hình cơ bản cho job
                var basicInfo = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    PerJobUserTimeLimit = long.MaxValue,
                    LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE | JOB_OBJECT_LIMIT_JOB_TIME
                };

                var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                {
                    BasicLimitInformation = basicInfo
                };

                // Cấp phát bộ nhớ và truyền thông tin đến job object
                IntPtr extendedInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION)));

                try
                {
                    Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

                    if (!SetInformationJobObject(
                        _jobHandle,
                        JobObjectInfoClass.JobObjectExtendedLimitInformation,
                        extendedInfoPtr,
                        (uint)Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION))))
                    {
                        int errorCode = Marshal.GetLastWin32Error();
                        throw new Win32Exception(errorCode, $"SetInformationJobObject failed with error code: {errorCode}");
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(extendedInfoPtr);
                }
            }
            catch (Exception)
            {
                _jobHandle.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Kiểm tra xem đối tượng đã bị dispose chưa, sử dụng Interlocked
        /// </summary>
        private bool IsDisposed()
        {
            return Interlocked.CompareExchange(ref _disposed, 0, 0) == 1;
        }

        /// <summary>
        /// Kiểm tra nếu đã dispose thì ném exception
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (IsDisposed())
                throw new ObjectDisposedException(nameof(ProcessSession));
        }

        /// <summary>
        /// Gán process để quản lý trong phiên làm việc.
        /// </summary>
        /// <param name="process">Process cần quản lý.</param>
        public void SetProcess(Process process)
        {
            if (process == null || process.HasExited)
            {
                throw new ArgumentException("Process has exited or does not exist");
            }

            ThrowIfDisposed();

            Process oldProcess = null;

            lock (_processLock)
            {
                oldProcess = _process;
                _process = process;
                _processId = process.Id;
                _processLifetime.Restart();

                // Bind ngay trong lock để đảm bảo process luôn được bind đúng
                try
                {
                    Bind(process);
                }
                catch (Exception bindEx)
                {
                    LogMessage($"Error binding process: {bindEx.Message}");
                    _process = oldProcess; // Restore previous process if binding fails
                    throw;
                }
            }

            // Xử lý process cũ bên ngoài lock để tránh deadlock
            if (oldProcess != null)
            {
                try
                {
                    CleanupProcess(oldProcess, 10000);
                }
                catch (Exception ex)
                {
                    LogMessage($"Error cleaning up previous process: {ex.Message}");
                }
            }
        }

        private void CleanupProcess(Process process, int timeoutMs)
        {
            if (process == null || process.HasExited)
                return;

            try
            {
                process.Terminator(timeoutMs);
            }
            finally
            {
                try
                {
                    process.Dispose();
                }
                catch
                {
                    // Bỏ qua lỗi khi dispose process
                }
            }
        }

        public void StartDataMonitor()
        {
            ThrowIfDisposed();
            //_dataProtector.Start();
        }

        /// <summary>
        /// Lấy process hiện tại đang được quản lý.
        /// </summary>
        /// <returns>Process đang được quản lý.</returns>
        public Process GetProcess()
        {
            ThrowIfDisposed();

            Process currentProcess;
            bool needRefresh = false;

            // Kiểm tra nhanh process hiện tại
            lock (_processLock)
            {
                currentProcess = _process;
                needRefresh = (currentProcess == null || currentProcess.HasExited);
            }

            // Nếu cần refresh, thực hiện bên ngoài lock chính
            if (needRefresh)
            {
                return RefreshProcess();
            }

            return currentProcess;
        }

        /// <summary>
        /// Kiểm tra xem process đã thoát hay chưa, sử dụng kiểm tra ngắn gọn.
        /// </summary>
        /// <returns>True nếu process đã thoát hoặc không tồn tại, ngược lại là False.</returns>
        public bool HasProcessExited()
        {
            if (IsDisposed())
                return true;

            Process currentProcess;

            // Sử dụng lock ngắn gọn chỉ để lấy tham chiếu process
            lock (_processLock)
            {
                currentProcess = _process;
                if (currentProcess == null)
                    return true;
            }

            try
            {
                // Kiểm tra HasExited và GetExitCodeProcess bên ngoài lock
                if (currentProcess.HasExited)
                    return true;

                // Sử dụng GetExitCodeProcess để kiểm tra trạng thái process
                if (GetExitCodeProcess(currentProcess.Handle, out uint exitCode))
                {
                    if (exitCode != STILL_ACTIVE)
                        return true;
                }

                return false;
            }
            catch (Exception)
            {
                // Nếu có lỗi khi truy cập process, coi như đã exit
                return true;
            }
        }

        /// <summary>
        /// Lấy thời gian process đã chạy tính bằng milliseconds.
        /// </summary>
        public long GetProcessLifetimeMs()
        {
            if (IsDisposed())
                return 0;

            long result = 0;

            lock (_processLock)
            {
                if (_process == null || _process.HasExited)
                    return 0;

                result = _processLifetime.ElapsedMilliseconds;
            }

            return result;
        }

        /// <summary>
        /// Làm mới đối tượng Process nếu cần thiết, thực hiện kiểm tra nhiều hơn bên ngoài lock
        /// </summary>
        private Process RefreshProcess()
        {
            if (IsDisposed())
                return null;

            Process currentProcess;
            int processId;

            lock (_processLock)
            {
                currentProcess = _process;
                processId = _processId;
            }

            // Nếu process hiện tại vẫn hoạt động, trả về luôn
            if (currentProcess != null && !currentProcess.HasExited)
            {
                return currentProcess;
            }

            // Cố gắng lấy lại process từ process ID bên ngoài lock
            if (processId > 0)
            {
                try
                {
                    Process refreshedProcess = Process.GetProcessById(processId);
                    if (refreshedProcess != null && !refreshedProcess.HasExited)
                    {
                        // Cập nhật lại _process với process mới tìm thấy
                        lock (_processLock)
                        {
                            _process = refreshedProcess;
                        }
                        return refreshedProcess;
                    }
                    else
                    {
                        // Process đã thoát hoặc không tồn tại
                        refreshedProcess?.Dispose();
                    }
                }
                catch (ArgumentException)
                {
                    // Process với ID này không tồn tại
                }
                catch (InvalidOperationException)
                {
                    // Process đã thoát
                }
            }

            // Trả về process hiện tại (có thể là null)
            return currentProcess;
        }

        /// <summary>
        /// Yêu cầu process thoát một cách an toàn, với ít lock hơn.
        /// </summary>
        /// <param name="timeoutMs">Thời gian chờ tối đa (milliseconds).</param>
        /// <returns>True nếu process đã thoát, False nếu không thể kết thúc process.</returns>
        private bool CloseProcess(int timeoutMs = 10000)
        {
            if (IsDisposed())
                return true;

            Process processToExit = null;

            // Chỉ lock khi lấy và xóa tham chiếu, thao tác exit thực hiện bên ngoài
            lock (_processLock)
            {
                processToExit = _process;
                _process = null;
            }

            if (processToExit == null || processToExit.HasExited)
                return true;

            bool processExited = false;

            try
            {
                // Sử dụng Terminator với timeout
                processToExit.Terminator(timeoutMs);
                processExited = processToExit.HasExited;
            }
            catch (Exception ex)
            {
                LogMessage($"Error during RequestExit: {ex.Message}");
            }
            finally
            {
                try
                {
                    processToExit?.Dispose();
                }
                catch (Exception disposeEx)
                {
                    LogMessage($"Error during Process Dispose: {disposeEx.Message}");
                }
            }

            return processExited;
        }

        /// <summary>
        /// Gán process vào job object để theo dõi và quản lý.
        /// </summary>
        private void Bind(Process process)
        {
            if (process is null || process.HasExited)
            {
                throw new ArgumentException("Bind progress: process has exited or does not exist");
            }

            if (!_jobHandle.IsInvalid)
            {
                bool success = AssignProcessToJobObject(_jobHandle, process.Handle);
                if (!success && !process.HasExited)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }

        /// <summary>
        /// Kiểm tra xem process có còn đang hoạt động bình thường không, với các kiểm tra bên ngoài lock.
        /// </summary>
        /// <returns>True nếu process đang hoạt động bình thường, False nếu không.</returns>
        public bool IsProcessHealthy()
        {
            if (IsDisposed())
                return false;

            Process currentProcess;

            // Chỉ sử dụng lock để lấy tham chiếu của process
            lock (_processLock)
            {
                currentProcess = _process;
                if (currentProcess == null || currentProcess.HasExited)
                    return false;
            }

            try
            {
                // Thực hiện tất cả kiểm tra bên ngoài lock
                bool processResponding = false;
                long memoryUsageMB = 0;
                TimeSpan currentProcessorTime = TimeSpan.Zero;

                try
                {
                    currentProcess.Refresh();
                    processResponding = currentProcess.Responding;
                    memoryUsageMB = currentProcess.WorkingSet64 / (1024 * 1024);
                    currentProcessorTime = currentProcess.TotalProcessorTime;
                }
                catch (Exception ex)
                {
                    LogMessage($"Error refreshing process info: {ex.Message}");
                    return false;
                }

                // Kiểm tra responding
                if (!processResponding)
                {
                    LogMessage($"Process {currentProcess.Id} is not responding.");
                    return false;
                }

                // Kiểm tra memory usage
                const long MAX_MEMORY_USAGE_MB = 1900; // 1.9GB
                if (memoryUsageMB > MAX_MEMORY_USAGE_MB)
                {
                    LogMessage($"Process {currentProcess.Id} memory usage too high: {memoryUsageMB}MB");
                    return false;
                }

                // Lấy và xử lý thông tin CPU kiểm tra lần trước
                ProcessCpuInfo lastCpuInfo = GetLastCpuInfo(currentProcess.Id);

                if (lastCpuInfo == null)
                {
                    // Lần đầu ghi nhận thông tin process này
                    UpdateCpuInfo(currentProcess.Id, DateTime.Now, currentProcessorTime);
                    return true;
                }

                // Tính toán CPU usage dựa trên lần check trước
                DateTime currentTime = DateTime.Now;
                double cpuUsagePercent = 0;
                TimeSpan timeDiff = currentTime - lastCpuInfo.LastCheckTime;

                if (timeDiff.TotalMilliseconds > 0) // Tránh chia cho 0
                {
                    cpuUsagePercent = (currentProcessorTime - lastCpuInfo.LastTotalProcessorTime).TotalMilliseconds /
                                       (Environment.ProcessorCount * timeDiff.TotalMilliseconds) * 100;
                }

                // Cập nhật thông tin cho lần check tiếp theo
                UpdateCpuInfo(currentProcess.Id, currentTime, currentProcessorTime);

                // Kiểm tra CPU usage
                const double MAX_CPU_USAGE = 90; // 90%
                if (cpuUsagePercent > MAX_CPU_USAGE)
                {
                    LogMessage($"Process {currentProcess.Id} CPU usage too high: {cpuUsagePercent:N1}%");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"Error checking process health: {ex.Message}");
                return false;
            }
        }

        // Phương thức tiện ích để ghi log
        private void LogMessage(string message)
        {
#if DEBUG
            Debug.WriteLine(message);
#endif
        }

        /// <summary>
        /// Lấy thông tin CPU cho process
        /// </summary>
        private ProcessCpuInfo GetLastCpuInfo(int processId)
        {
            ProcessCpuInfo lastCpuInfo = null;
            lock (_lastCpuCheckInfo)
            {
                _lastCpuCheckInfo.TryGetValue(processId, out lastCpuInfo);
            }
            return lastCpuInfo;
        }

        /// <summary>
        /// Cập nhật thông tin CPU cho process
        /// </summary>
        private void UpdateCpuInfo(int processId, DateTime checkTime, TimeSpan processorTime)
        {
            lock (_lastCpuCheckInfo)
            {
                _lastCpuCheckInfo[processId] = new ProcessCpuInfo
                {
                    LastCheckTime = checkTime,
                    LastTotalProcessorTime = processorTime
                };
            }
        }

        /// <summary>
        /// Giải phóng tài nguyên và kết thúc process.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Sử dụng Interlocked.CompareExchange để chỉ dispose một lần
            if (disposing && Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                try
                { 
                    CloseProcess();
                }
                catch (Exception ex)
                {
                    LogMessage($"Error during RequestExit in Dispose: {ex.Message}");
                }
                finally
                {
                    try
                    {
                        if (_jobHandle != null && !_jobHandle.IsInvalid)
                        {
                            _jobHandle.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error disposing job handle: {ex.Message}");
                    }
                }
            }
        }

        ~ProcessSession()
        {
            Dispose(false);
        }

        // Lưu trữ thông tin CPU
        private static readonly Dictionary<int, ProcessCpuInfo> _lastCpuCheckInfo =
            new Dictionary<int, ProcessCpuInfo>();

        private class ProcessCpuInfo
        {
            public DateTime LastCheckTime { get; set; }
            public TimeSpan LastTotalProcessorTime { get; set; }
        }
    }
}