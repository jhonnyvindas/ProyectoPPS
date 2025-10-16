using System.Globalization;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PasarelaPago.Shared.Dtos;
using Syncfusion.Blazor.Grids;
using Syncfusion.Blazor.Navigations;





namespace PasarelaPago.Client.Pages.DashboardPagos
{
    public partial class DashboardPagos : ComponentBase
    {
        [Inject] public HttpClient Http { get; set; } = default!;
        [Inject] public IJSRuntime JSRuntime { get; set; } = default!;

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

        protected override async Task OnInitializedAsync() => await CargarDashboard();

        public async Task CargarDashboard()
        {
            await CargarDatos(1, 1000); 
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

        protected async Task ToolbarClick(ClickEventArgs args)
        {
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

        private static string BuildQuery(IDictionary<string, string?> query)
        {
            var parts = query
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}")
                .ToArray();

            return parts.Length > 0 ? "?" + string.Join("&", parts) : string.Empty;
        }

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
    
        protected static string ToCapitalizedStatus(string? estado)
        {
            if (string.IsNullOrWhiteSpace(estado)) return string.Empty;

            var trimmed = estado.Trim().ToLowerInvariant();

            if (trimmed.Length == 0) return string.Empty;

            return char.ToUpperInvariant(trimmed[0]) + trimmed.Substring(1);
        }
        protected static string GetStatusCssClass(string? estadoCapitalizado)
        {
            if (string.IsNullOrWhiteSpace(estadoCapitalizado)) return string.Empty;

            var estado = estadoCapitalizado.Trim().ToLowerInvariant();

            return estado switch
            {
                "aprobado" => "estado-aprobado",
                "rechazado" => "estado-rechazado",
                _ => string.Empty, 
            };
        }

        public void ExcelQueryCellInfoHandler(ExcelQueryCellInfoEventArgs<DTOTransacciones> args)
        {
            if (args.Data is null || args.Column is null) return;
            var col = args.Column.HeaderText;

            if (args.Column.HeaderText == "Estado")
            {
                if (args.Cell.Value.ToString().Equals("aprobado"))
                {
                    args.Cell.CellStyle.FontColor = "#69bb19"; 
                }
                else if (args.Cell.Value.ToString().Equals("rechazado"))
                {
                    args.Cell.CellStyle.FontColor = "#dc143c"; 
                }
            }

            // País (CR -> Costa Rica)
            if (args.Column.Field == nameof(DTOTransacciones.pais))
            {
                args.Cell.Value = ToCountryName(args.Data.pais);
                return;
            }

            // Moneda (CRC -> Colones)
            if (args.Column.Field == nameof(DTOTransacciones.moneda))
            {
                args.Cell.Value = ToCurrencyName(args.Data.moneda);
                return;
            }


        }


        private void ExcelQueryCellInfo(ExcelQueryCellInfoEventArgs<DTOTransacciones> args)
        {
            if (args.Data is null || args.Column is null) return;

            // País (CR -> Costa Rica)
            if (args.Column.Field == nameof(DTOTransacciones.pais))
            {
                args.Cell.Value = ToCountryName(args.Data.pais);
                return;
            }

            // Moneda (CRC -> Colones)
            if (args.Column.Field == nameof(DTOTransacciones.moneda))
            {
                args.Cell.Value = ToCurrencyName(args.Data.moneda);
                return;
            }

            // Monto como número con formato (sin alinear)
            if (args.Column.Field == nameof(DTOTransacciones.monto))
            {
                args.Cell.Value = Convert.ToDouble(args.Data.monto);
                if (args.Style != null)
                {
                    args.Style.NumberFormat = "#,##0.00"; // CellStyle sí soporta NumberFormat
                }
                return;
            }

            // Fecha con el mismo formato visual de la tabla
            if (args.Column.Field == nameof(DTOTransacciones.fechaTransaccion))
            {
                args.Cell.Value = args.Data.fechaTransaccion?.ToString("dd/MM/yyyy HH:mm");
                return;
            }

            // Estado con color (verde/rojo) y negrita
            if (args.Column.Field == nameof(DTOTransacciones.estadoTransaccion))
            {
                var estado = ToCapitalizedStatus(args.Data.estadoTransaccion); // “Aprobado”/“Rechazado”
                args.Cell.Value = estado;

                if (args.Style != null)
                {
                    args.Style.Bold = true;
                    args.Style.FontColor = estado.Equals("Aprobado", StringComparison.OrdinalIgnoreCase)
                        ? "#198754"   // verde
                        : estado.Equals("Rechazado", StringComparison.OrdinalIgnoreCase)
                            ? "#dc3545" // rojo
                            : "#000000";
                }
                return;
            }
        }

    }
}

