using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using PasarelaPago.Shared.Dtos;
using Microsoft.JSInterop;

namespace PasarelaPago.Client.Pages.DashboardPagos
{
    public partial class DashboardPagos : ComponentBase
    {

        [Inject] public HttpClient Http { get; set; } = default!;
        [Inject] public IJSRuntime JSRuntime { get; set; } = default!;


        public string SearchString { get; set; } = string.Empty;
        public DTOTransacciones? SelectedTransaccion { get; set; }


        public List<DTOTransacciones> Transacciones { get; set; } = new();
        public FiltroTransacciones Filtro { get; set; } =
            new()
            {
                FechaInicio = DateTime.Today.AddDays(-1),
                FechaFin = DateTime.Today
            };

        protected static readonly string[] SearchFields = new[]
        {
            nameof(DTOTransacciones.cedula),
            nameof(DTOTransacciones.nombreCliente),
            nameof(DTOTransacciones.pais),
            nameof(DTOTransacciones.numeroOrden),
            nameof(DTOTransacciones.estadoTransaccion),
            nameof(DTOTransacciones.moneda)
        };

        // --- Mapeo de Países y Monedas (Se mantiene sin cambios) ---

        protected static readonly IReadOnlyDictionary<string, string> CountryMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["CR"] = "Costa Rica",
                ["PA"] = "Panamá",
                ["CO"] = "Colombia"
            };

        protected static string ToCountryName(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return "";
            return CountryMap.TryGetValue(code.Trim(), out var n) ? n : code!;
        }

        protected static readonly IReadOnlyDictionary<string, (string Nombre, string Simbolo, CultureInfo Culture)>
        CurrencyMap = new Dictionary<string, (string, string, CultureInfo)>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = ("Dólares", "$", new CultureInfo("en-US")),
            ["CRC"] = ("Colones", "₡", new CultureInfo("es-CR")),
        };

        protected static string ToCurrencyName(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return "";
            return CurrencyMap.TryGetValue(code.Trim(), out var x) ? x.Nombre : code!;
        }

        protected static string CurrencySymbol(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return "";
            return CurrencyMap.TryGetValue(code.Trim(), out var x) ? x.Simbolo : "";
        }

        protected static string FormatMoney(decimal amount, string? code)
        {
            if (!string.IsNullOrWhiteSpace(code) && CurrencyMap.TryGetValue(code.Trim(), out var x))
                return amount.ToString("N2", x.Culture);
            return amount.ToString("N2", CultureInfo.InvariantCulture);
        }

        // -------------------------------------------------------------------
        // INICIO DE LA LÓGICA DE CARGA Y EXPORTACIÓN
        // -------------------------------------------------------------------

        protected override async Task OnInitializedAsync() => await CargarDashboard();

        public async Task CargarDashboard()
        {
            // Para el filtro de búsqueda se llama a CargarDatos con página 1 y un tamaño grande (ej: 1000)
            await CargarDatos(1, 1000);
        }

        // ** EL NUEVO MÉTODO DE EXPORTACIÓN A EXCEL SIN SYNCFUSION **
        public async Task ExportarAExcel()
        {
            try
            {
                var baseUri = Http.BaseAddress ?? new Uri("http://localhost/");
                var endpoint = new Uri(baseUri, "api/Transaccion/exportar-excel").ToString();

                var query = new Dictionary<string, string?>();

                // Fechas: Se envía solo la fecha para que el servidor maneje la conversión a UTC y AddDays.
                if (Filtro.FechaInicio.HasValue)
                    query["fechaInicio"] = Filtro.FechaInicio.Value.Date.ToString("yyyy-MM-dd");

                if (Filtro.FechaFin.HasValue)
                {
                    query["fechaFin"] = Filtro.FechaFin.Value.Date.ToString("yyyy-MM-dd");
                }

                // 🚨 CORRECCIÓN CLAVE: Usar directamente Filtro.EstadoTransaccion
                if (!string.IsNullOrWhiteSpace(Filtro.EstadoTransaccion))
                {
                    query["estadoTransaccion"] = Filtro.EstadoTransaccion;
                }

                // Si tienes filtro de búsqueda para el Excel, úsalo aquí
                if (!string.IsNullOrWhiteSpace(SearchString))
                {
                    query["busqueda"] = SearchString;
                }

                var finalUrl = endpoint + BuildQuery(query);

                Console.WriteLine($"[Exportar] GET {finalUrl}");

                // 1. Llama a la API para obtener el archivo como un stream de bytes.
                var response = await Http.GetAsync(finalUrl, HttpCompletionOption.ResponseHeadersRead);

                // ... (el manejo de la respuesta se mantiene) ...
                if (response.IsSuccessStatusCode)
                {
                    var fileName = response.Content.Headers.ContentDisposition?.FileNameStar ??
                                   response.Content.Headers.ContentDisposition?.FileName ??
                                   "Transacciones.xlsx";

                    var fileBytes = await response.Content.ReadAsByteArrayAsync();

                    // Llamada a JS para descargar el archivo
                    await JSRuntime.InvokeVoidAsync("BlazorDownloadFile", fileName, fileBytes);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error HTTP al exportar: {response.StatusCode} - {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al exportar a Excel: " + ex.Message);
            }
        }

        // -------------------------------------------------------------------
        // LÓGICA DE CARGA DE DATOS (CORREGIDA)
        // -------------------------------------------------------------------

        private async Task CargarDatos(int pagina, int tamanio)
        {
            try
            {
                var baseUri = Http.BaseAddress ?? new Uri("http://localhost/");
                var endpoint = new Uri(baseUri, "api/Transaccion/dashboard").ToString();

                var query = new Dictionary<string, string?>
                {
                    ["pagina"] = pagina.ToString(),
                    ["tamanio"] = tamanio.ToString()
                };

                // Fechas
                if (Filtro.FechaInicio.HasValue)
                    // Envía la fecha/hora completa con formato ISO ('o')
                    query["fechaInicio"] = Filtro.FechaInicio.Value.ToString("o");

                if (Filtro.FechaFin.HasValue)
                {
                    // La API espera el inicio del día, AddDays(1).AddTicks(-1) ya no es necesario aquí.
                    // Se deja solo el valor del DatePicker para que el servidor lo maneje.
                    query["fechaFin"] = Filtro.FechaFin.Value.ToString("o");
                }

                // Búsqueda
                if (!string.IsNullOrWhiteSpace(SearchString))
                    query["busqueda"] = SearchString;

                // 🚨 CORRECCIÓN CLAVE: Usar directamente Filtro.EstadoTransaccion
                if (!string.IsNullOrWhiteSpace(Filtro.EstadoTransaccion))
                {
                    query["estadoTransaccion"] = Filtro.EstadoTransaccion;
                }

                var finalUrl = endpoint + BuildQuery(query);
                Console.WriteLine($"[Transacciones] GET {finalUrl}");

                var result = await Http.GetFromJsonAsync<PaginacionResponse<DTOTransacciones>>(finalUrl);
                var data = result?.Resultados ?? new List<DTOTransacciones>();

                Transacciones = data;

                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al cargar transacciones: " + ex.Message);
            }
        }

        // --- Métodos Auxiliares (MapEstadoToApi fue ELIMINADO ya que era la causa del error) ---

        // NormalizeStatus se mantiene, ya que se usa para colorear en el Razor.
        private static string NormalizeStatus(string? v)
        {
            var s = (v ?? "").Trim().ToLowerInvariant();
            if (s is "success" or "approved" or "aprobado" or "captured" or "completed" or "paid")
                return "aprobado";
            if (s is "failed" or "rechazado" or "declined" or "canceled" or "cancelled" or "error")
                return "rechazado";
            return s;
        }

        private static string BuildQuery(IDictionary<string, string?> query)
        {
            var parts = query
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}")
                .ToArray();

            return parts.Length > 0 ? "?" + string.Join("&", parts) : string.Empty;
        }

        // [NUEVO] Método QuickFilter para MudBlazor (filtro en el cliente)
        public bool QuickFilter(DTOTransacciones element)
        {
            if (string.IsNullOrWhiteSpace(SearchString))
                return true;

            // Revisa si alguno de los campos de búsqueda contiene la cadena.
            var searchLower = SearchString.ToLowerInvariant();

            return (element.cedula?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true) ||
                   (element.nombreCliente?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true) ||
                   (element.pais?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true) ||
                   (element.numeroOrden?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true) ||
                   (element.estadoTransaccion?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true) ||
                   (element.moneda?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true);
        }
    }
}