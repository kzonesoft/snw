using Kzone.Engine.Controller.Infrastructure.Helpers;
using System;
using System.Diagnostics;

namespace Kzone.Engine.Controller.Application.Extensions
{
    internal static class ProcessExtension
    {
        public static void Terminator(this Process process, int timeWaitToExit = 10000)
        {
            if (process == null || process.HasExited) return;

            try
            {
                // Gọi Kill để kết thúc tiến trình
                process.Kill();

                // Đợi tiến trình kết thúc với thời gian chờ được chỉ định
                if (!process.WaitForExit(timeWaitToExit))
                {
                    // Nếu tiến trình vẫn chưa thoát sau thời gian chờ
                    var processId = process?.Id;
                    if (processId != null)
                    {
                        // Chạy lệnh taskkill để ép buộc kết thúc tiến trình
                        CmdHelper.RunCommand($"taskkill /PID {process.Id} /F");
                    }
                }
            }
            // Có thể ghi log hoặc xử lý ngoại lệ ....
            catch (InvalidOperationException ex)
            {
                // Tiến trình đã kết thúc trước khi gọi Kill hoặc không hợp lệ
                Console.WriteLine($"Process termination failed: {ex.Message}");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // Tiến trình không thể bị kill, có thể do không có quyền
                Console.WriteLine($"Failed to kill process (likely due to permissions): {ex.Message}");
            }
        }

        internal static bool GetProcessById(int processId, out Process process)
        {
            process = null;
            try
            {
                process = Process.GetProcessById(processId);
                if (process == null || process.HasExited)
                    return false;
                return true;

            }
            catch
            {
                return false;
            }
        }
    }
}
