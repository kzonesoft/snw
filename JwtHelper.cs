using Jose;
using System;
using System.Collections.Generic;
using System.Text;

namespace KzoneSyncService.Infrastructure.Networks.WebServer
{
    public static class JwtHelper
    {
        private const string SecretKey = "uNCxWzDI5M3mmEElAZ99126m";

        public static string GenerateJwtToken()
        {
            var payload = new
            {
                exp = GetUnixTimeAfterMinutes(60 * 24) // Thời gian hết hạn
            };

            var key = Encoding.UTF8.GetBytes(SecretKey);

            return JWT.Encode(payload, key, JwsAlgorithm.HS256);
        }

        public static bool ValidateJwtToken(string token)
        {
            try
            {
                // Giải mã và kiểm tra chữ ký
                var key = Encoding.UTF8.GetBytes(SecretKey);
                var payload = JWT.Decode<Dictionary<string, object>>(token, key, JwsAlgorithm.HS256);

                // Kiểm tra trường exp
                if (!payload.TryGetValue("exp", out var expObj) || !long.TryParse(expObj.ToString(), out var exp))
                {
                    // Không có trường exp hoặc giá trị không hợp lệ => token không hợp lệ
                    return false;
                }

                // Kiểm tra thời gian hết hạn
                var expirationTime = GetDateTimeFromUnixTime(exp);
                if (DateTime.UtcNow > expirationTime)
                {
                    // Token đã hết hạn
                    return false;
                }

                // Token hợp lệ
                return true;
            }
            catch (JoseException ex)
            {
                // Token không hợp lệ (lỗi chữ ký, định dạng sai, v.v.)
                Console.WriteLine($"Token invalid: {ex.Message}");
                Console.WriteLine(token);
                return false;
            }
            catch (Exception ex)
            {
                // Lỗi không mong muốn
                Console.WriteLine($"Unexpected error: {ex.Message}");
                return false;
            }
        }



        private static long GetUnixTimeAfterMinutes(int minutes)
        {
            // Lấy thời gian hiện tại UTC
            DateTime utcNow = DateTime.UtcNow;

            // Cộng thêm số phút
            DateTime futureTime = utcNow.AddMinutes(minutes);

            // Epoch time bắt đầu từ 1/1/1970 UTC
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Tính Unix timestamp (giây)
            return (long)(futureTime - epoch).TotalSeconds;
        }

        private static DateTime GetDateTimeFromUnixTime(long unixTimestamp)
        {
            // Epoch time bắt đầu từ 1/1/1970 UTC
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Cộng thêm số giây của Unix timestamp
            return epoch.AddSeconds(unixTimestamp);
        }
    }
}
