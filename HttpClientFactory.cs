using System;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Kzone.Engine.Controller.Infrastructure.Helpers
{
    public static class HttpClientFactory
    {
        private static readonly HttpClient _client;

        static HttpClientFactory()
        {
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _client.Timeout = TimeSpan.FromSeconds(30);
        }

        public static HttpClient CreateClient()
        {
            return _client;
        }

        public static HttpClient CreateClient(string baseUrl, string username, string password)
        {
            var client = CreateClient();
            client.BaseAddress = new Uri(baseUrl);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
            }

            return client;
        }
    }
}