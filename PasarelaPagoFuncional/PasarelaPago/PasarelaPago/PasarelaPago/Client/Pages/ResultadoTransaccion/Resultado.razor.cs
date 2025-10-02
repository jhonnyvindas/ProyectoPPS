using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
// Asegúrate de que estas referencias DTOs y Models existan en tu proyecto
using PasarelaPago.Shared.Dtos;
using PasarelaPago.Shared.Models;

namespace PasarelaPago.Client.Pages.ResultadoTransaccion;

public partial class Resultado : ComponentBase
{
    // INYECCIÓN DE DEPENDENCIAS
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    public bool Loading { get; private set; } = true;
    public PaymentResult Result { get; private set; } = new();
    public string? PersistenciaEstado { get; private set; } // Estado de la operación de BD

    // Banner UI
    protected string BannerCss { get; private set; } = "banner-success";
    protected string BannerIcon { get; private set; } = MudBlazor.Icons.Material.Filled.CheckCircle;
    protected string BannerText { get; private set; } = "¡Pago aprobado!";

    // Lógica principal
    protected override async Task OnInitializedAsync()
    {
        // 1. Leer los parámetros de la URL
        ParseFromUrl();

        // 2. Persistir el resultado en la BD (la solución al BUG)
        await PersistirResultadoAsync();

        Loading = false;
    }

    private void ParseFromUrl()
    {
        var uri = new Uri(Nav.Uri);
        var dict = ParseQuery(uri.Query);

        // Datos core que envía Tilopay
        dict.TryGetValue("code", out var code);
        dict.TryGetValue("description", out var description);
        dict.TryGetValue("auth", out var auth);
        dict.TryGetValue("order", out var order);
        dict.TryGetValue("brand", out var brand);
        dict.TryGetValue("last-digits", out var last4);
        dict.TryGetValue("tilopay-transaction", out var tilopayTx);
        if (string.IsNullOrWhiteSpace(tilopayTx) && dict.TryGetValue("tpt", out var tpt))
            tilopayTx = tpt;

        // Monto / moneda
        dict.TryGetValue("amount", out var amount);
        dict.TryGetValue("currency", out var currency);

        // Cliente
        dict.TryGetValue("customer", out var customer);
        dict.TryGetValue("email", out var email);
        dict.TryGetValue("billToFirstName", out var firstName);
        dict.TryGetValue("billToLastName", out var lastName);

        // País y Cédula
        dict.TryGetValue("billToCountry", out var country);
        if (string.IsNullOrWhiteSpace(country) && dict.TryGetValue("country", out var c2)) country = c2;
        dict.TryGetValue("customerId", out var idNumber);
        if (string.IsNullOrWhiteSpace(idNumber) && dict.TryGetValue("cedula", out var c3)) idNumber = c3;
        if (string.IsNullOrWhiteSpace(idNumber) && dict.TryGetValue("id", out var c4)) idNumber = c4;

        var displayCustomer = !string.IsNullOrWhiteSpace(customer)
          ? customer
          : $"{(firstName ?? "").Trim()} {(lastName ?? "").Trim()}".Trim();

        // Usamos el código o el estado crudo para la BD
        dict.TryGetValue("status", out var statusRaw);
        if (string.IsNullOrWhiteSpace(statusRaw)) statusRaw = code;


        Result = new PaymentResult
        {
            Code = code,
            Description = description,
            Auth = auth,
            Order = order,
            Brand = brand,
            Last4 = last4,
            TilopayTx = tilopayTx,
            Amount = amount,
            Currency = currency,
            Customer = customer,
            FirstName = firstName,
            LastName = lastName,
            DisplayCustomer = string.IsNullOrWhiteSpace(displayCustomer) ? null : displayCustomer,
            Country = country,
            IdNumber = idNumber,
            Email = email,
            StatusRaw = statusRaw, // Propiedad que usamos para la persistencia
        };

        // Lógica del Banner
        var status = Classify(code, description);
        switch (status)
        {
            case PaymentStatus.Success:
                BannerCss = "banner-success";
                BannerIcon = MudBlazor.Icons.Material.Filled.CheckCircle;
                BannerText = "¡Pago aprobado!";
                break;
            case PaymentStatus.Pending:
                BannerCss = "banner-warning";
                BannerIcon = MudBlazor.Icons.Material.Filled.HourglassBottom;
                BannerText = "Pago en proceso";
                break;
            default:
                BannerCss = "banner-error";
                BannerIcon = MudBlazor.Icons.Material.Filled.Error;
                BannerText = "Pago rechazado";
                break;
        }
    }

    // --- MÉTODO PARA PERSISTIR EL RESULTADO EN LA BD ---
    private async Task PersistirResultadoAsync()
    {
        // CORRECCIÓN: Usamos Order, TilopayTx o Auth como identificadores mínimos.
        // Si no hay identificación de Tilopay (Tx o Auth) y tampoco Order, es probable que la transacción haya fallado
        // tan pronto que no tiene sentido guardarla.
        if (string.IsNullOrWhiteSpace(Result.Order) || (string.IsNullOrWhiteSpace(Result.TilopayTx) && string.IsNullOrWhiteSpace(Result.Auth)))
        {
            PersistenciaEstado = "No se requiere guardar (datos incompletos o transacción fallida/cancelada sin ID de Tilopay).";
            return;
        }

        try
        {
            PersistenciaEstado = "Guardando resultado en la base de datos...";
            StateHasChanged();

            // 1. Normalizar el estado
            var estadoBD = NormalizarEstadoParaBD(Result.StatusRaw);

            // 2. Crear los modelos Cliente y Pago para enviar al API
            var cliente = new Cliente
            {
                cedula = (Result.IdNumber ?? "").Trim(),
                nombre = Result.FirstName ?? "",
                apellido = Result.LastName ?? "",
                correo = Result.Email,
                pais = Result.Country // Añadido para completar el modelo Cliente
            };

            var pago = new Pago
            {
                numeroOrden = Result.Order!, // Sabemos que Order no es null por la validación
                cedula = (Result.IdNumber ?? "").Trim(),
                metodoPago = "payfac",
                // El monto y moneda siempre son requeridos
                monto = decimal.TryParse(Result.Amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var m) ? m : 0m,
                moneda = (Result.Currency ?? "USD").ToUpperInvariant(),
                estadoTilopay = estadoBD,
                numeroAutorizacion = Result.Auth,
                // Guardamos los datos recibidos de la URL como un JSON simple
                datosRespuestaTilopay = $"{{ \"code\": \"{Result.Code}\", \"description\": \"{Result.Description}\", \"auth\": \"{Result.Auth}\", \"tx_id\": \"{Result.TilopayTx}\", \"order\": \"{Result.Order}\" }}",
                fechaTransaccion = DateTime.UtcNow,
                marcaTarjeta = (Result.Brand ?? "").ToLowerInvariant(),
            };

            var payload = new PagoConCliente { Cliente = cliente, Pago = pago };

            // 3. Llamar a tu endpoint de servidor para guardar/actualizar
            var resp = await Http.PostAsJsonAsync("api/Transaccion", payload);
            if (!resp.IsSuccessStatusCode)
            {
                var reason = $"{(int)resp.StatusCode} {resp.ReasonPhrase}";
                throw new InvalidOperationException($"Error al guardar datos: {reason}");
            }

            PersistenciaEstado = $"Transacción {Result.Order} guardada como '{estadoBD.ToUpper()}'.";
        }
        catch (Exception ex)
        {
            PersistenciaEstado = $"ERROR DE PERSISTENCIA: {ex.Message}";
        }
        finally
        {
            StateHasChanged();
        }
    }

    // Función para clasificar el estado de Tilopay en un estado de BD simple
    private static string NormalizarEstadoParaBD(string? statusRaw)
    {
        var s = (statusRaw ?? "").Trim().ToLowerInvariant();

        // 1. El código '1' o la palabra 'success' o 'approved' indica éxito.
        if (s == "1" || s == "success" || s == "approved")
            return "aprobado";

        // 2. Códigos de pendiente/revisión
        if (s == "pending" || s == "review")
            return "pendiente";

        // El resto es considerado 'rechazado' (0, error, timeout, etc.)
        return "rechazado";
    }

    // --- Métodos Auxiliares ---
    private static Dictionary<string, string> ParseQuery(string? query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query)) return result;

        var trimmed = query.StartsWith("?") ? query[1..] : query;
        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            var key = HttpUtility.UrlDecode(kv[0]) ?? "";
            var val = kv.Length > 1 ? HttpUtility.UrlDecode(kv[1]) ?? "" : "";
            result[key] = val;
        }
        return result;
    }

    private static PaymentStatus Classify(string? code, string? description)
    {
        var c = (code ?? "").Trim();
        if (c == "1" || c.Equals("success", StringComparison.OrdinalIgnoreCase))
            return PaymentStatus.Success;

        if (c == "0" && description?.Contains("pend", StringComparison.OrdinalIgnoreCase) == true)
            return PaymentStatus.Pending;

        return PaymentStatus.Error;
    }

    protected void VolverInicio() => Nav.NavigateTo("/");

    protected static string FormatBrand(string? brand, string? last4)
    {
        var b = (brand ?? "").Trim();
        var mask = !string.IsNullOrWhiteSpace(last4) ? $"•••• •••• •••• {last4}" : "";
        if (string.IsNullOrWhiteSpace(b))
            return string.IsNullOrWhiteSpace(mask) ? "-" : mask;

        b = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(b.ToLowerInvariant());
        return string.IsNullOrWhiteSpace(mask) ? b : $"{b} — {mask}";
    }

    // --- Definición de la Clase Resultado (Corregida de CS1061) ---
    public sealed class PaymentResult
    {
        // tilopay
        public string? Code { get; set; }
        public string? Description { get; set; }
        public string? Auth { get; set; }
        public string? Order { get; set; }
        public string? Brand { get; set; }
        public string? Last4 { get; set; }
        public string? TilopayTx { get; set; }
        public string? StatusRaw { get; set; }

        // monto
        public string? Amount { get; set; }
        public string? Currency { get; set; }

        // cliente
        public string? Customer { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? DisplayCustomer { get; set; }
        public string? Country { get; set; }
        public string? IdNumber { get; set; }
        public string? Email { get; set; }

        public string? AmountLabel =>
          !string.IsNullOrWhiteSpace(Amount) && !string.IsNullOrWhiteSpace(Currency)
            ? FormatAmount(Amount!, Currency!)
            : null;

        private static string FormatAmount(string raw, string currency)
        {
            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                var culture = currency.ToUpperInvariant() switch
                {
                    "CRC" => new CultureInfo("es-CR"),
                    "USD" => new CultureInfo("en-US"),
                    _ => CultureInfo.InvariantCulture
                };
                var symbol = currency.ToUpperInvariant() switch
                {
                    "CRC" => "₡",
                    "USD" => "$",
                    _ => currency.ToUpperInvariant() + " "
                };
                return symbol + value.ToString("N2", culture);
            }
            return $"{currency} {raw}";
        }
    }

    private enum PaymentStatus { Success, Pending, Error }
}