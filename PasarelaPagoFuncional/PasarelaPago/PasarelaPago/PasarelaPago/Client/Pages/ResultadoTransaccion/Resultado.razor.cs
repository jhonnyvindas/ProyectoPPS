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
    [Inject] NavigationManager Nav { get; set; }
    [Inject] private HttpClient Http { get; set; } = default!;

    // 2) Parámetro de ruta
    [Parameter] public string? token { get; set; }

    // 3) Estado de pantalla
    public bool Loading { get; set; } = true;
    public string? Error { get; set; }
    private ResultadoPagoDto? Data { get; set; }

    // 4) Banner
    public string BannerCss { get; set; } = "banner-warning";
    public string BannerIcon { get; set; } = MudBlazor.Icons.Material.Filled.Info;
    public string BannerText { get; set; } = "Procesando respuesta…";

    protected override async Task OnInitializedAsync()
    {
        // A) Quitar la query string de la barra (oculta code, description, brand, etc.)
        var uri = new Uri(Nav.Uri);
        if (!string.IsNullOrEmpty(uri.Query))
        {
            var clean = uri.GetLeftPart(UriPartial.Path);
            Nav.NavigateTo(clean, replace: true); // actualiza URL sin recargar
        }

        // B) Validar que tenemos token en la ruta
        if (string.IsNullOrWhiteSpace(token))
        {
            Error = "Token no provisto en la ruta.";
            Loading = false;
            return;
        }

        // C) Cargar DTO por token (tu endpoint ya normaliza y persiste el estado)
        try
        {
            var dto = await Http.GetFromJsonAsync<ResultadoPagoDto>($"api/Transaccion/resultado/{token}");
            if (dto is null)
            {
                Error = "No se pudo obtener el resultado de la transacción.";
                return;
            }

            Data = dto;
            SetBannerFromEstado(dto.Estado);

            // D) (Opcional) Redirigir a URL limpio por número de orden
            // Si quieres permanecer en esta vista, comenta esta línea:
            // Nav.NavigateTo($"/pagos/orden/{dto.NumeroOrden}", replace: true);
        }
        catch (Exception ex)
        {
            Error = $"Error al consultar el resultado: {ex.Message}";
        }
        finally
        {
            Loading = false;
        }
    }

    private void SetBannerFromEstado(string? estado)
    {
        var e = (estado ?? "").Trim().ToLowerInvariant();
        if (e == "aprobado")
        {
            BannerCss = "banner-success";
            BannerIcon = MudBlazor.Icons.Material.Filled.CheckCircle;
            BannerText = "Transacción aprobada";
        }
        else if (e == "pendiente")
        {
            BannerCss = "banner-warning";
            BannerIcon = MudBlazor.Icons.Material.Filled.Schedule;
            BannerText = "Transacción en revisión";
        }
        else
        {
            BannerCss = "banner-error";
            BannerIcon = MudBlazor.Icons.Material.Filled.Error;
            BannerText = "Transacción rechazada";
        }
        // Forzar re-render del banner ya calculado
        StateHasChanged();
    }

    // Botón “Volver al inicio”
    private void VolverInicio()
    { 
        try{
        Nav.NavigateTo("/pagos/tilopay", forceLoad: true);
        
        }
        catch(Exception ex){
            var a = ex; 
        }
    }




    // País a nombre legible (puedes ampliar este mapa)
    private static string GetCountryName(string? code)
        => (code ?? "").Trim().ToUpperInvariant() switch
        {
            "CR" => "Costa Rica",
            "CO" => "Colombia",
            "PA" => "Panamá",
            _ => code ?? "-"
        };

    // DTO mínimo para esta vista (coincide con tu controlador)
    private sealed class ResultadoPagoDto
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
