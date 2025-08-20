using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Text.Json;

namespace PasarelaPago.Server.Controllers;

[ApiController]
[Route("api/tilopay")] 
public class TilopayController : ControllerBase
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;

    public TilopayController(HttpClient http, IConfiguration cfg)
    {
        _http = http;
        _cfg = cfg;
    }

    // GET /api/tilopay/config-check
    [HttpGet("config-check")]
    public IActionResult ConfigCheck()
    {
        var apiUser = _cfg["Tilopay:ApiUser"];
        var apiPassword = _cfg["Tilopay:ApiPassword"];
        var url = _cfg["Tilopay:SdkTokenUrl"];
        return Ok(new
        {
            ApiUser = string.IsNullOrWhiteSpace(apiUser) ? "MISSING" : "OK",
            ApiPassword = string.IsNullOrWhiteSpace(apiPassword) ? "MISSING" : "OK",
            SdkTokenUrl = string.IsNullOrWhiteSpace(url) ? "MISSING" : url
        });
    }

    // Controllers/TilopayController.cs
    [HttpPost("sdk-token")]
    public async Task<IActionResult> GetSdkToken()
    {
        var apiUser = _cfg["Tilopay:ApiUser"];
        var apiPassword = _cfg["Tilopay:ApiPassword"];
        var apiKey = _cfg["Tilopay:ApiKey"];
        var url = _cfg["Tilopay:SdkTokenUrl"]; // https://app.tilopay.com/api/v1/loginSdk

        if (string.IsNullOrWhiteSpace(apiUser) ||
            string.IsNullOrWhiteSpace(apiPassword) ||
            string.IsNullOrWhiteSpace(apiKey))
            return StatusCode(500, "Faltan Tilopay:ApiUser/ApiPassword/ApiKey.");
        if (string.IsNullOrWhiteSpace(url))
            return StatusCode(500, "Falta Tilopay:SdkTokenUrl.");

        var payload = new { apiuser = apiUser, password = apiPassword, key = apiKey };

        var resp = await _http.PostAsJsonAsync(url, payload);
        var raw = await resp.Content.ReadAsStringAsync();

        // No lances excepción: reenvía status y cuerpo tal cual para diagnóstico
        return StatusCode((int)resp.StatusCode, raw);
    }

}
