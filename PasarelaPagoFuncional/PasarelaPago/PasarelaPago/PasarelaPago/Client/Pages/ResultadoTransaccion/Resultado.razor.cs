using Microsoft.AspNetCore.Components;
using MudBlazor;
using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PasarelaPago.Client.Pages.ResultadoTransaccion;

public partial class Resultado : ComponentBase
{
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    [Parameter] public string token { get; set; } = default!;

    public bool Loading { get; private set; } = true;
    public string? Error { get; private set; }
    public ResultadoPagoDto? Data { get; private set; }

    protected string BannerCss { get; private set; } = "banner-success";
    protected string BannerIcon { get; private set; } = Icons.Material.Filled.CheckCircle;
    protected string BannerText { get; private set; } = "¡Pago aprobado!";

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var currentUri = new Uri(Nav.Uri);
            var qs = currentUri.Query;

            var apiUrl = string.IsNullOrEmpty(qs)
                ? $"api/Transaccion/resultado/{token}"
                : $"api/Transaccion/resultado/{token}{qs}";

            var resp = await Http.GetAsync(apiUrl);
            if (!resp.IsSuccessStatusCode)
            {
                var msg = await resp.Content.ReadAsStringAsync();
                Error = $"No fue posible obtener el resultado ({(int)resp.StatusCode}): {msg}";
                return;
            }

            Data = await resp.Content.ReadFromJsonAsync<ResultadoPagoDto>();
            if (Data is null)
            {
                Error = "No se encontró el resultado.";
                return;
            }

            var s = (Data.Estado ?? "").Trim().ToLowerInvariant();
            var aprobado = s is "aprobado" or "success" or "approved" or "1";
            var pendiente = s is "pendiente" or "pending" or "review";

            if (aprobado)
            {
                BannerCss = "banner-success";
                BannerIcon = Icons.Material.Filled.CheckCircle;
                BannerText = "¡Pago aprobado!";
            }
            else if (pendiente)
            {
                BannerCss = "banner-warning";
                BannerIcon = Icons.Material.Filled.HourglassBottom;
                BannerText = "Pago en proceso";
            }
            else
            {
                BannerCss = "banner-error";
                BannerIcon = Icons.Material.Filled.Error;
                BannerText = "Pago rechazado";
            }
        }
        catch (HttpRequestException ex)
        {
            Error = $"No fue posible obtener el resultado: {ex.Message}";
        }
        finally
        {
            Loading = false;
        }
    }

    protected void VolverInicio() => Nav.NavigateTo("/");

    protected static string FormatBrand(string? brand, string? masked)
    {
        var b = (brand ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(b))
            b = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(b.ToLowerInvariant());

        return string.IsNullOrWhiteSpace(masked)
            ? (string.IsNullOrWhiteSpace(b) ? "-" : b)
            : (string.IsNullOrWhiteSpace(b) ? masked : $"{b} — {masked}");
    }

    protected static string GetCountryName(string? codeOrName)
    {
        var s = (codeOrName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(s))
            return "-";

        var c = s.ToUpperInvariant();

        return c switch
        {
            "CR" => "Costa Rica",
            "CO" => "Colombia",
            "PA" => "Panamá",
            _ => s.Length <= 3
                    ? c                        
                    : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLowerInvariant()) 
        };
    }

    public sealed class ResultadoPagoDto
    {
        public string NumeroOrden { get; set; } = default!;
        public string? Cedula { get; set; }
        public string? Estado { get; set; }
        public decimal Monto { get; set; }
        public string Moneda { get; set; } = "CRC";
        public string? NumeroAutorizacion { get; set; }
        public string? MarcaTarjeta { get; set; }
        public DateTime FechaTransaccion { get; set; }
        public string? Nombre { get; set; }
        public string? Apellido { get; set; }
        public string? DisplayCustomer { get; set; }
        public string? Email { get; set; }
        public string? Pais { get; set; }
        public string? TilopayTx { get; set; }
    }
}
