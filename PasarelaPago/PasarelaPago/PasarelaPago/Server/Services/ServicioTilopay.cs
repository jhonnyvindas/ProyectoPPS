using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PasarelaPago.Shared.Models;

public class TilopayService
{
    private readonly HttpClient _http;
    private readonly ConfiguracionTilopay _config;

    public TilopayService(HttpClient http, IOptions<ConfiguracionTilopay> config)
    {
        _http = http;
        _config = config.Value;
        _http.BaseAddress = new Uri(_config.baseUrl); // Usa la URL desde appsettings.json
    }

    // Obtener el token usando las credenciales
    private async Task<string> ObtenerTokenAsync()
    {
        var requestBody = new
        {
            llaveApi = _config.llaveApi,
            usuarioApi = _config.usuarioApi,
            contrasenaApi = _config.contrasenaApi
        };

        var response = await _http.PostAsJsonAsync("v1/login", requestBody);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Error al obtener token: {response.StatusCode}, Detalles: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<Token>();
        return result?.access_token ?? throw new Exception("No se recibió el token de acceso");
    }

    // Obtener lista de transacciones usando token
    public async Task<List<Transaccion>> ObtenerTransaccionesAsync()
    {
        var token = await ObtenerTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "v1/transacciones");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        request.Content = JsonContent.Create(new
        {
            fechaInicio = "2025-01-01",
            fechaFin = "2025-08-05"
        });

        var response = await _http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Error al consultar transacciones: {response.StatusCode}, {error}");
        }

        var data = await response.Content.ReadFromJsonAsync<List<Transaccion>>();
        return data ?? new List<Transaccion>();
    }
}
