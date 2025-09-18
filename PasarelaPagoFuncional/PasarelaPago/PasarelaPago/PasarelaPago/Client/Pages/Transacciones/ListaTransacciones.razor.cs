using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using PasarelaPago.Shared.Dtos;
using Syncfusion.Blazor.Grids;
using Syncfusion.Blazor.Navigations;
using GridAction = Syncfusion.Blazor.Grids.Action;

namespace PasarelaPago.Client.Pages.DashboardPagos
{
    public partial class DashboardPagos : ComponentBase
    {

        [Inject] public HttpClient Http { get; set; } = default!;


        protected SfGrid<DTOTransacciones>? GridTransacciones;
        public List<DTOTransacciones> Transacciones { get; set; } = new();
        public FiltroTransacciones Filtro { get; set; } =
    new()
    {
        FechaInicio = DateTime.Today.AddDays(-1), 
        FechaFin = DateTime.Today              
    };

        private DTOTransacciones? _sel;


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
                return x.Simbolo + amount.ToString("N2", x.Culture); // ₡1.234,56 ó $1,234.56
            return amount.ToString("N2", CultureInfo.InvariantCulture);
        }
        protected override async Task OnInitializedAsync() => await CargarDashboard();

        public async Task CargarDashboard()
        {
            var pageSize = GridTransacciones?.PageSettings.PageSize ?? 10;
            await CargarDatos(1, pageSize);
        }

        protected async Task OnActionBegin(ActionEventArgs<DTOTransacciones> args)
        {
            if (args.RequestType is GridAction.BeginEdit or GridAction.Add or GridAction.Save)
                return;

            if (args.RequestType is GridAction.Paging or GridAction.Sorting or GridAction.Filtering or GridAction.Searching)
            {
                var currentPage = args.CurrentPage > 0
                    ? args.CurrentPage
                    : (GridTransacciones?.PageSettings.CurrentPage ?? 1);

                var pageSize = GridTransacciones?.PageSettings.PageSize ?? 10;
                await CargarDatos(currentPage, pageSize);
            }
        }

        protected void OnRowSelected(RowSelectEventArgs<DTOTransacciones> args) => _sel = args.Data;
        protected void OnRowDeselected() => _sel = null;

        public async Task OnToolbarClick(ClickEventArgs args)
        {
            if (args.Item.Id == "GridTransacciones_excelexport")
            {
                var props = new ExcelExportProperties
                {
                    IncludeTemplateColumn = true,
                    FileName = "Transacciones.xlsx",
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
                                        RowSpan = 1,
                                        ColSpan = 8, 
                                        Value   = "Listado de Transacciones",
                                        Style   = new ExcelStyle { Bold = true, Italic = true, FontSize = 13 }
                                    }
                                }
                            }
                        }
                    }
                };

                await GridTransacciones!.ExportToExcelAsync(props);
            }
        }

        public void OnExcelQueryCellInfo(ExcelQueryCellInfoEventArgs<DTOTransacciones> args)
        {
            switch (args.Column.Field)
            {
                case nameof(DTOTransacciones.estadoTransaccion):
                    {
                        var s = (args.Data.estadoTransaccion ?? "").Trim().ToLowerInvariant();
                        if (s is "success" or "approved" or "aprobado" or "captured" or "completed" or "paid")
                            args.Cell.Value = "Aprobado";
                        else if (s is "failed" or "rechazado" or "declined" or "canceled" or "cancelled" or "error")
                            args.Cell.Value = "Rechazado";
                        else
                            args.Cell.Value = args.Data.estadoTransaccion ?? "";
                        break;
                    }

                case nameof(DTOTransacciones.pais):
                    args.Cell.Value = ToCountryName(args.Data.pais);
                    break;

                case nameof(DTOTransacciones.moneda):
                    args.Cell.Value = ToCurrencyName(args.Data.moneda); 
                    break;

                case nameof(DTOTransacciones.monto):
                    args.Cell.Value = FormatMoney(args.Data.monto, args.Data.moneda); 
                    break;

                case nameof(DTOTransacciones.fechaTransaccion):
                    if (args.Data.fechaTransaccion.HasValue)
                        args.Cell.Value = args.Data.fechaTransaccion.Value.ToString("dd/MM/yyyy HH:mm");
                    break;
            }
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
                {
                    var fin = Filtro.FechaFin.Value.Date.AddDays(1).AddTicks(-1);
                    query["fechaFin"] = fin.ToString("o");
                }

                if (!string.IsNullOrWhiteSpace(Filtro.Busqueda))
                    query["busqueda"] = Filtro.Busqueda;

                if (!string.IsNullOrWhiteSpace(Filtro.EstadoTransaccion))
                {
                    var estadoApi = MapEstadoToApi(Filtro.EstadoTransaccion); 
                    if (!string.IsNullOrEmpty(estadoApi))
                        query["estadoTransaccion"] = estadoApi;
                }

                var finalUrl = endpoint + BuildQuery(query);
                Console.WriteLine($"[Transacciones] GET {finalUrl}");

                var result = await Http.GetFromJsonAsync<PaginacionResponse<DTOTransacciones>>(finalUrl);
                var data = result?.Resultados ?? new List<DTOTransacciones>();

                if (!string.IsNullOrWhiteSpace(Filtro.EstadoTransaccion))
                {
                    var objetivo = NormalizeStatus(Filtro.EstadoTransaccion);
                    data = data.FindAll(t => NormalizeStatus(t.estadoTransaccion) == objetivo);
                }

                Transacciones = data;

                if (GridTransacciones is not null)
                {
                    GridTransacciones.TotalItemCount = result?.TotalRegistros ?? data.Count;
                    await GridTransacciones.Refresh();
                }

                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al cargar transacciones: " + ex.Message);
            }
        }

        private static string? MapEstadoToApi(string? ui)
        {
            if (string.IsNullOrWhiteSpace(ui)) return null;
            var s = ui.Trim().ToLowerInvariant();
            if (s == "aprobado") return "success";
            if (s == "rechazado") return "failed";
            return s;
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

        private static string BuildQuery(IDictionary<string, string?> query)
        {
            var parts = query
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}")
                .ToArray();

            return parts.Length > 0 ? "?" + string.Join("&", parts) : string.Empty;
        }
    }
}
