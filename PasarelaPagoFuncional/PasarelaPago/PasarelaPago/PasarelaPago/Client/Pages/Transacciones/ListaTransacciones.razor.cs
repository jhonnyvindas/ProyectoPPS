// PasarelaPago.Client/Pages/DashboardPagos/Transacciones.razor.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PasarelaPago.Shared.Dtos;

// Syncfusion
using Syncfusion.Blazor.Grids;
using Syncfusion.Blazor.Navigations;

namespace PasarelaPago.Client.Pages.DashboardPagos
{
    public partial class DashboardPagos : ComponentBase
    {
        [Inject] public HttpClient Http { get; set; } = default!;
        [Inject] public IJSRuntime JSRuntime { get; set; } = default!;

        // Referencia al grid
        protected SfGrid<DTOTransacciones> GridTransacciones { get; set; } = default!;

        public string SearchString { get; set; } = string.Empty;
        public DTOTransacciones? SelectedTransaccion { get; set; }

        public List<DTOTransacciones> Transacciones { get; set; } = new();

        public FiltroTransacciones Filtro { get; set; } = new()
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

        // -------------------------------------------------------
        // Ciclo de vida y carga de datos
        // -------------------------------------------------------
        protected override async Task OnInitializedAsync() => await CargarDashboard();

        public async Task CargarDashboard()
        {
            await CargarDatos(1, 1000); // página 1, tamaño grande
        }

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

                if (Filtro.FechaInicio.HasValue)
                    query["fechaInicio"] = Filtro.FechaInicio.Value.ToString("o");

                if (Filtro.FechaFin.HasValue)
                    query["fechaFin"] = Filtro.FechaFin.Value.ToString("o");

                if (!string.IsNullOrWhiteSpace(SearchString))
                    query["busqueda"] = SearchString;

                if (!string.IsNullOrWhiteSpace(Filtro.EstadoTransaccion))
                    query["estadoTransaccion"] = Filtro.EstadoTransaccion;

                var finalUrl = endpoint + BuildQuery(query);
                Console.WriteLine($"[Transacciones] GET {finalUrl}");

                var result = await Http.GetFromJsonAsync<PaginacionResponse<DTOTransacciones>>(finalUrl);
                Transacciones = result?.Resultados ?? new List<DTOTransacciones>();

                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al cargar transacciones: " + ex.Message);
            }
        }

        // -------------------------------------------------------
        // Exportación a Excel integrada de Syncfusion
        // -------------------------------------------------------
        protected async Task ToolbarClick(ClickEventArgs args)
        {
            // El ID del botón es <IDGRID>_excelexport; verifica que coincida con tu ID del grid
            if (args.Item.Id?.Contains("GridTransacciones_excelexport", StringComparison.OrdinalIgnoreCase) == true
                || string.Equals(args.Item.Text, "ExcelExport", StringComparison.OrdinalIgnoreCase))
            {
                var props = new ExcelExportProperties
                {
                    FileName = $"Transacciones_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                    IncludeTemplateColumn = true,
                    Header = new ExcelHeader
                    {
                        HeaderRows = 1,
                        Rows = new List<ExcelRow>
                        {
                            new ExcelRow
                            {
                                Index = 1,
                                Cells = new List<ExcelCell>
                                {
                                    // Estilo básico (evitamos tipos que varían por versión)
                                    new ExcelCell
                                    {
                                        ColSpan = 8,
                                        RowSpan = 1,
                                        Value = "Lista de Transacciones",
                                        Style = new ExcelStyle
                                        {
                                            Bold = true,
                                            Italic = true,
                                            FontSize = 13
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                await GridTransacciones.ExportToExcelAsync(props);
            }
        }

        // -------------------------------------------------------
        // Auxiliares
        // -------------------------------------------------------
        private static string BuildQuery(IDictionary<string, string?> query)
        {
            var parts = query
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}")
                .ToArray();

            return parts.Length > 0 ? "?" + string.Join("&", parts) : string.Empty;
        }

        // Mantengo por si lo usas en otras vistas (no se usa en SfGrid directamente)
        public bool QuickFilter(DTOTransacciones element)
        {
            if (string.IsNullOrWhiteSpace(SearchString))
                return true;

            var s = SearchString.ToLowerInvariant();

            return (element.cedula?.Contains(s, StringComparison.OrdinalIgnoreCase) == true) ||
                   (element.nombreCliente?.Contains(s, StringComparison.OrdinalIgnoreCase) == true) ||
                   (element.pais?.Contains(s, StringComparison.OrdinalIgnoreCase) == true) ||
                   (element.numeroOrden?.Contains(s, StringComparison.OrdinalIgnoreCase) == true) ||
                   (element.estadoTransaccion?.Contains(s, StringComparison.OrdinalIgnoreCase) == true) ||
                   (element.moneda?.Contains(s, StringComparison.OrdinalIgnoreCase) == true);
        }

        private static string NormalizeStatus(string? v)
        {
            var s = (v ?? "").Trim().ToLowerInvariant();
            if (s is "success" or "approved" or "aprobado" or "captured" or "completed" or "paid")
                return "aprobado";
            if (s is "failed" or "rechazado" or "declined" or "canceled" or "cancelled" or "error")
                return "rechazado";
            return s;
        }
    }
}
