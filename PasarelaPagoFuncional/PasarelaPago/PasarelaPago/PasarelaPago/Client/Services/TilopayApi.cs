using System.Text.Json;

namespace PasarelaPago.Client.Services  // <- ESTE NAMESPACE
{
    public class TilopayApi
    {
        private readonly HttpClient _http;

        public TilopayApi(HttpClient http)
        {
            _http = http;
            // Ajusta al HTTPS del Server
            _http.BaseAddress = new Uri("https://localhost:7295/");
        }

        public async Task<string?> GetSdkTokenAsync()
        {
            using var resp = await _http.PostAsync("api/tilopay/sdk-token", content: null);
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"[sdk-token] status={(int)resp.StatusCode} body={json}");
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("access_token", out var t)) return t.GetString();
            if (doc.RootElement.TryGetProperty("token", out var t2)) return t2.GetString();
            return null;
        }
    }
}
