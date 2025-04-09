using System;
using System.Diagnostics;
using System.IO;

namespace Kzone.Engine.Controller.Infrastructure.Helpers
{
    internal class CmdHelper
    {
        /// <summary>
        /// Thực thi một lệnh command thông qua cmd.exe.
        /// </summary>
        /// <param name="command">Lệnh cần thực thi.</param>
        /// <param name="timeWaitToKill">
        /// Thời gian chờ (ms) trước khi buộc kết thúc tiến trình. 
        /// Nếu bằng 0 thì chờ vô thời hạn.
        /// </param>
        public static void RunCommand(string command, int timeWaitToKill = 0)
        {
            RunCmdWindows(command, timeWaitToKill);
        }

        /// <summary>
        /// Thực thi một file batch thông qua cmd.exe.
        /// </summary>
        /// <param name="batFilePath">Đường dẫn file .bat cần thực thi.</param>
        /// <param name="timeWaitToKill">Thời gian chờ (ms) trước khi buộc kết thúc tiến trình.</param>
        public static void RunBatch(string batFilePath, int timeWaitToKill = 30000)
        {
            if (!File.Exists(batFilePath))
            {
                Console.WriteLine($"Not found bat file : {batFilePath}");
                return;
            }
            RunCmdWindows(batFilePath, timeWaitToKill);
        }

        /// <summary>
        /// Thực thi một lệnh hoặc file thông qua cmd.exe và xử lý đầu ra.
        /// </summary>
        /// <param name="argument">Lệnh hoặc đường dẫn file để thực thi.</param>
        /// <param name="timeWaitToKill">Thời gian chờ (ms) trước khi buộc kết thúc tiến trình.</param>
        private static void RunCmdWindows(string argument, int timeWaitToKill)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{argument}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = processInfo })
            {
                // Đăng ký xử lý đầu ra chuẩn
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Console.WriteLine(e.Data);
                };

                // Đăng ký xử lý lỗi
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Console.Error.WriteLine(e.Data);
                };

                process.Start();

                // Bắt đầu đọc luồng output và error
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Nếu có giới hạn thời gian chờ, kiểm tra và kill tiến trình nếu vượt quá
                if (timeWaitToKill > 0)
                {
                    if (!process.WaitForExit(timeWaitToKill))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Unable to terminate process : {ex.Message}");
                        }
                    }
                    else
                    {
                        // Đảm bảo tiến trình đã hoàn toàn kết thúc
                        process.WaitForExit();
                    }
                }
                else
                {
                    // Chờ vô thời hạn nếu timeWaitToKill = 0
                    process.WaitForExit();
                }
            }
        }

        /// <summary>
        /// Chạy một chuỗi lệnh BAT (có thể nhiều dòng) mà không cần ghi ra file.
        /// </summary>
        /// <param name="batCommands">Chuỗi lệnh bat cần chạy.</param>
        /// <param name="timeWaitToKill">Thời gian chờ trước khi kết thúc tiến trình (ms).</param>
        public static void RunBatchString(string batCommands, int timeWaitToKill = 30000)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                RedirectStandardInput = true,    // Cho phép ghi dữ liệu vào tiến trình
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                if (process == null)
                {
                    Console.WriteLine("Không thể khởi động tiến trình cmd.exe.");
                    return;
                }

                // Bắt đầu đọc luồng output và error (nếu cần)
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Gửi chuỗi lệnh vào tiến trình thông qua StandardInput.
                using (var sw = process.StandardInput)
                {
                    if (sw.BaseStream.CanWrite)
                    {
                        // Ghi chuỗi lệnh (có thể chứa nhiều dòng)
                        sw.WriteLine(batCommands);
                        // Ghi lệnh "exit" để kết thúc phiên CMD sau khi thực hiện các lệnh
                        sw.WriteLine("exit");
                    }
                }

                // Chờ tiến trình hoàn thành, nếu vượt quá thời gian quy định thì kết thúc tiến trình.
                if (timeWaitToKill > 0 && !process.WaitForExit(timeWaitToKill) && !process.HasExited)
                {
                    process.Kill();
                }
                process.Close();
            }
        }

        public static void RunBatchCommandsSequentially(string batCommands, int timeWaitToKill = 30000)
        {
            // Tách các lệnh theo dòng (bỏ qua dòng trống nếu có)
            string[] commands = batCommands.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var command in commands)
            {
                // Sử dụng /C để chạy lệnh và sau đó kết thúc CMD
                var processInfo = new ProcessStartInfo("cmd.exe", "/C " + command)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process == null)
                    {
                        Console.WriteLine($"Không thể chạy lệnh: {command}");
                        continue;
                    }
                    // Đọc output/error nếu cần (có thể sử dụng process.BeginOutputReadLine() …)
                    if (!process.WaitForExit(timeWaitToKill))
                    {
                        process.Kill();
                    }
                }
            }
        }
    }
}
