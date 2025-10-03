using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PasarelaPago.Client.Services;
using PasarelaPago.Shared.Dtos;
using PasarelaPago.Shared.Models;
using System.Globalization;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PasarelaPago.Client.Pages.PagosTilopay;

public partial class PagoTilopay : ComponentBase, IAsyncDisposable
{
    [Inject] protected TilopayApi Api { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] protected HttpClient Http { get; set; } = default!;

    // --- Configuración de Retardo para UI (5 Segundos) ---
    private readonly TimeSpan _minWaitTime = TimeSpan.FromSeconds(5);
    // ---------------------------------------------------

    public string? SelectedPaymentMethod { get; set; } = null;
    public bool IsPayfac => (SelectedPaymentMethod?.Contains(":payfac:", StringComparison.OrdinalIgnoreCase) ?? false);

    // Tarjeta (UI)
    public string? CardNumber { get; set; } = "4012000000020089";
    public string? Expiry { get; set; } = "12/26";
    public string? CVV { get; set; } = "123";

    // Datos del comprador
    public bool OwnerReadOnly { get; } = false;
    public string? BillToFirstName { get; set; } = "Demo";
    public string? BillToLastName { get; set; } = "User";
    public string? CustomerId { get; set; } = "1232323232";
    public string? BillToTelephone { get; set; }
    public string? BillToEmail { get; set; } = "demo@example.com";
    public string? BillToCountry { get; set; } = "CR";
    public string? BillToZipPostCode { get; set; }
    public string? BillToCity { get; set; } = "San José";
    public string? BillToState { get; set; } = "SJ";
    public string? BillToAddress { get; set; } = "123 Main Street";

    // Marca de tarjeta
    public string? CardBrand { get; set; }
    public int CvvMaxLen => CardBrand == "amex" ? 4 : 3;
    public int CardNumberMaxLen => CardBrand == "amex" ? 15 : 19;
    public int CardNumberMaxChars => CardBrand == "amex" ? 17 : 22;
    public int CardNumberDigitsMax => CardBrand == "amex" ? 15 : 19;

    public string? ValidateCardNumber(string? v)
    {
        var digits = new string((v ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length < 13 || digits.Length > CardNumberDigitsMax)
            return $"Número de tarjeta inválido ({digits.Length}/{CardNumberDigitsMax})";
        return null;
    }

    private string _orderNumber = Guid.NewGuid().ToString("N");
    public string OrderNumber => _orderNumber;

    // Monto
    public decimal Amount { get; set; } = 10.00m;
    public string Currency { get; set; } = "USD";
    public string AmountString => Amount.ToString("F2", CultureInfo.InvariantCulture);

    public string RedirectUrl => BuildRedirectUrl("/pagos/resultado");
    public bool Pagando { get; set; }
    public string? Estado { get; set; }

    private DotNetObjectReference<PagoTilopay>? _selfRef;

    // Modal de error/timeout
    private bool ShowFailModal { get; set; } = false;
    private string? FailReason { get; set; }

    // Almacena el tiempo en que se hizo clic en pagar (para calcular el retardo)
    private DateTime? _paymentStartTime;

    public string PayLabel => FormatPayLabel(Amount, Currency);
    public static string FormatPayLabel(decimal amount, string? currency)
    {
        currency = currency?.ToUpperInvariant();
        var symbol = currency switch { "CRC" => "₡", "USD" => "$", _ => "" };
        var culture = currency switch
        {
            "CRC" => new CultureInfo("es-CR"),
            "USD" => new CultureInfo("en-US"),
            _ => CultureInfo.InvariantCulture
        };
        return symbol + amount.ToString("N2", culture);
    }

    // Attrs UI
    public IReadOnlyDictionary<string, object> CustomerIdInputAttrs => new Dictionary<string, object>
    {
        ["id"] = "customerId",
        ["name"] = "customerId",
        ["inputmode"] = "numeric",
        ["maxlength"] = "12",
        ["pattern"] = "[0-9]*"
    };
    public IReadOnlyDictionary<string, object> BillToTelephoneInputAttrs => new Dictionary<string, object>
    {
        ["id"] = "billToTelephone",
        ["name"] = "billToTelephone",
        ["inputmode"] = "numeric",
        ["maxlength"] = "9",
        ["pattern"] = "[0-9]*"
    };
    public IReadOnlyDictionary<string, object> CountryAttrs => new Dictionary<string, object>
    { ["id"] = "billToCountry", ["name"] = "billToCountry" };
    public IReadOnlyDictionary<string, object> ZipAttrs => new Dictionary<string, object>
    { ["id"] = "billToZipPostCode", ["name"] = "billToZipPostCode", ["inputmode"] = "numeric", ["pattern"] = "[0-9]*" };
    public IReadOnlyDictionary<string, object> CityAttrs => new Dictionary<string, object>
    { ["id"] = "billToCity", ["name"] = "billToCity" };
    public IReadOnlyDictionary<string, object> StateAttrs => new Dictionary<string, object>
    { ["id"] = "billToState", ["name"] = "billToState" };
    public IReadOnlyDictionary<string, object> Address1Attrs => new Dictionary<string, object>
    { ["id"] = "billToAddress", ["name"] = "billToAddress" };

    protected override void OnInitialized()
    {
        _selfRef = DotNetObjectReference.Create(this);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        if (_selfRef is null)
            _selfRef = DotNetObjectReference.Create(this);

        try
        {
            var token = await Api.GetSdkTokenAsync();
            if (!string.IsNullOrWhiteSpace(token))
            {
                var preOptions = new
                {
                    orderNumber = OrderNumber,
                    amount = AmountString,
                    currency = Currency,
                    description = "preinit",
                    language = "es",
                    capture = "1",
                    redirect = BuildRedirectUrl("/pagos/resultado"),
                };

                await JS.InvokeVoidAsync("tilopayInterop.ensureInit", token, preOptions, _selfRef);
                await JS.InvokeVoidAsync("tilopayInterop.watchCardBrand", _selfRef);
            }
        }
        catch { }

        await JS.InvokeVoidAsync("tilopayInterop.watchCardBrand", _selfRef);
    }

    public async ValueTask DisposeAsync()
    {
        _selfRef?.Dispose();
        await Task.CompletedTask;
    }

    private string BuildRedirectUrl(string path) => BuildRedirectUrl(path, null);
    private string BuildRedirectUrl(string path, IDictionary<string, string?>? query)
    {
        var uri = new Uri(Nav.Uri);
        var scheme = "https";
        var host = uri.Host;
        var port = uri.IsDefaultPort ? "" : $":{uri.Port}";
        var baseUrl = $"{scheme}://{host}{port}{path}";
        if (query is null || query.Count == 0) return baseUrl;

        var parts = new List<string>(query.Count);
        foreach (var kv in query)
        {
            if (string.IsNullOrWhiteSpace(kv.Key)) continue;
            if (string.IsNullOrWhiteSpace(kv.Value)) continue;
            parts.Add($"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}");
        }
        var qs = string.Join("&", parts);
        return parts.Count > 0 ? $"{baseUrl}?{qs}" : baseUrl;
    }

    // Validaciones
    public string? ValidateExpiry(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return "Fecha requerida";
        var parts = v.Split('/');
        if (parts.Length != 2) return "Formato MM/YY";
        if (!int.TryParse(parts[0], out var mm) || mm < 1 || mm > 12) return "Mes inválido";
        if (!int.TryParse(parts[1], out var yy)) return "Año inválido";
        var now = DateTime.UtcNow; var curYY = now.Year % 100; var curMM = now.Month;
        var ok = (yy > curYY) || (yy == curYY && mm >= curMM);
        return ok ? null : "Tarjeta vencida";
    }
    public string? ValidateCVV(string? v)
    {
        var digits = new string((v ?? "").Where(char.IsDigit).ToArray());
        return digits.Length == CvvMaxLen ? null : $"CVV de {CvvMaxLen} dígitos";
    }
    public string? ValidatePhone(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return "Teléfono requerido";
        var d = new string(v.Where(char.IsDigit).ToArray());
        return d.Length == 8 ? null : "Use formato ####-####";
    }
    public string? ValidateConcept(string? v) => string.IsNullOrWhiteSpace(v) || v.Trim().Length < 3 ? "Mínimo 3 caracteres" : null;
    public string? ValidateZip(string? v) => string.IsNullOrWhiteSpace(v) ? "Requerido" : null;
    public string? ValidateCity(string? v) => string.IsNullOrWhiteSpace(v) ? "Requerido" : null;
    public string? ValidateState(string? v) => string.IsNullOrWhiteSpace(v) ? "Requerido" : null;
    public string? ValidateAddress(string? v) => string.IsNullOrWhiteSpace(v) ? "Requerido" : null;

    public string ZipPlaceholder =>
    (BillToCountry ?? "CR").ToUpperInvariant() switch
    {
        "CR" => "10101",
        "PA" => "0801",
        "CO" => "110111",
        _ => "Código postal"
    };

    private string ToInvariantAmount(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture);
    private string MetodoPagoParaBD() => "payfac";

    // Helpers modal/recarga (manual con botón OK)
    private async Task Reload()
    {
        try { await JS.InvokeVoidAsync("tilopayInterop.hardReload"); }
        catch { Nav.NavigateTo(Nav.Uri, forceLoad: true); }
    }
    private void ShowFailure(string reason)
    {
        FailReason = reason;
        ShowFailModal = true;
        StateHasChanged();
    }
    private async Task OnFailOk()
    {
        ShowFailModal = false;
        StateHasChanged();
        await Reload();
    }

    public async Task Pagar()
    {
        if (Pagando) return;
        _paymentStartTime = DateTime.UtcNow; // Guardamos el tiempo de inicio
        Pagando = true;
        ShowFailModal = false;

        try
        {
            if (string.IsNullOrWhiteSpace(CustomerId))
            {
                Estado = "Debe indicar la cédula del cliente.";
                return;
            }

            Estado = "Obteniendo token…";
            StateHasChanged();

            var token = await Api.GetSdkTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                Estado = "No se obtuvo token del servidor.";
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedPaymentMethod) || !IsPayfac)
            {
                try
                {
                    var payfac = await JS.InvokeAsync<string>("tilopayInterop.getPayfacMethod");
                    if (!string.IsNullOrWhiteSpace(payfac))
                        SelectedPaymentMethod = payfac;
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(SelectedPaymentMethod) || !IsPayfac)
            {
                Estado = "No hay método de tarjeta disponible para la moneda seleccionada.";
                return;
            }

            _orderNumber = Guid.NewGuid().ToString("N");
            StateHasChanged();

            Estado = "Inicializando SDK…";
            StateHasChanged();

            var redirectUrl = BuildRedirectUrl("/pagos/resultado", new Dictionary<string, string?>
            {
                ["amount"] = ToInvariantAmount(Amount),
                ["currency"] = Currency,
                ["billToFirstName"] = BillToFirstName,
                ["billToLastName"] = BillToLastName,
                ["billToCountry"] = BillToCountry,
                ["customerId"] = CustomerId,
                ["email"] = BillToEmail
            });

            var options = new
            {
                orderNumber = _orderNumber,
                amount = ToInvariantAmount(Amount),
                currency = Currency,
                description = "Pago de servicios",
                language = "es",
                capture = "1",
                redirect = redirectUrl,
                billToEmail = BillToEmail,
                billToFirstName = BillToFirstName,
                billToLastName = BillToLastName,
                billToAddress = BillToAddress,
                billToCity = BillToCity,
                billToCountry = BillToCountry,
                billToState = BillToState,
                billToZipPostCode = BillToZipPostCode,
                billToTelephone = BillToTelephone,
                subscription = 0,
                hashVersion = "V2",
                paymentMethod = SelectedPaymentMethod
            };

            await JS.InvokeVoidAsync("tilopayInterop.ensureInit", token, options, _selfRef);

            try
            {
                var brand = await JS.InvokeAsync<string>("tilopayInterop.getCardType");
                if (!string.IsNullOrWhiteSpace(brand))
                {
                    CardBrand = brand.ToLowerInvariant();
                    StateHasChanged();
                }
            }
            catch { }

            Estado = "Esperando confirmación del banco…";
            StateHasChanged();

            // Timeout aumentado a 120 segundos para la pasarela
            await JS.InvokeVoidAsync("tilopayInterop.prepareAndPayWithTimeout", 120000);
        }
        catch (JSException jse)
        {
            Estado = $"Error JS/SDK: {jse.Message}";
            ShowFailure("Ocurrió un problema con el SDK. Intente nuevamente.");
        }
        catch (Exception ex)
        {
            Estado = $"Error: {ex.Message}";
            ShowFailure("Ocurrió un problema inesperado. Intente nuevamente.");
        }
        finally
        {
            Pagando = false;
            StateHasChanged();
        }
    }

    // --- Callbacks desde JS ---

    public sealed class PaymentEvent
    {
        public string? status { get; set; }
        public object? payload { get; set; }
    }

    // EN PagoTilopay.razor.cs

    [JSInvokable]
    public async Task OnPaymentEvent(PaymentEvent evt)
    {
        var status = (evt?.status ?? "").ToLowerInvariant();

        try
        {
            if (status == "approved")
            {
                // ... (código existente para "approved")
            }
            else
            {
                var payloadString = evt?.payload?.ToString() ?? status;

                // 1. Detección de errores específicos de Tilopay (Payload con JSON)
                //    Buscamos un error de validación de tarjeta dentro del JSON.
                bool isCardValidationError = payloadString.Contains("número de tarjeta inválido", StringComparison.OrdinalIgnoreCase) ||
                                             payloadString.Contains("tarjeta vencida", StringComparison.OrdinalIgnoreCase) ||
                                             payloadString.Contains("CVV", StringComparison.OrdinalIgnoreCase) ||
                                             payloadString.Contains("número de tarjeta válido", StringComparison.OrdinalIgnoreCase) || // <--- ESTE ES EL NUEVO CHECK
                                             payloadString.Contains("Card not allowed", StringComparison.OrdinalIgnoreCase); // <--- Para errores de test

                string failReasonMessage;

                if (isCardValidationError)
                {
                    // Mensaje amigable para el MODAL (Transacción no efectuada)
                    failReasonMessage = "Por favor verifique los datos de la tarjeta.";

                    // Mensaje simple para la ALERTA DE ESTADO (oculta el JSON técnico)
                    Estado = $"Pago rechazado: Error de datos de tarjeta.";
                }
                else
                {
                    // Si es cualquier otro error (fondos insuficientes, denegación, etc.)
                    failReasonMessage = "La transacción no pudo ser efectuada. Intente nuevamente.";

                    // Mantiene el estado original, que puede incluir el mensaje del banco/pasarela
                    Estado = $"Pago rechazado o no confirmado ({status}): {payloadString}";
                }

                // Lógica de retardo forzado (MIN WAIT TIME)
                if (_paymentStartTime.HasValue)
                {
                    var elapsed = DateTime.UtcNow - _paymentStartTime.Value;
                    var remainingDelay = _minWaitTime - elapsed;

                    if (remainingDelay.TotalMilliseconds > 0)
                    {
                        await Task.Delay(remainingDelay);
                    }
                }

                ShowFailure(failReasonMessage); // Mostrar modal con el mensaje amigable o genérico
            }
        }
        catch (Exception ex)
        {
            // ... (código existente para manejo de excepción)
        }

        StateHasChanged();
    }

    [JSInvokable]
    public async Task OnPaymentTimeout()
    {
        // Lógica de retardo forzado (MIN WAIT TIME)
        if (_paymentStartTime.HasValue)
        {
            var elapsed = DateTime.UtcNow - _paymentStartTime.Value;
            var remainingDelay = _minWaitTime - elapsed;

            if (remainingDelay.TotalMilliseconds > 0)
            {
                // Espera el tiempo restante para cumplir el mínimo de 5 segundos de espera
                await Task.Delay(remainingDelay);
            }
        }

        ShowFailure("Tiempo de espera agotado. No fue posible completar la transacción.");
    }

    [JSInvokable]
    public Task OnCardBrandChanged(string brand)
    {
        CardBrand = string.IsNullOrWhiteSpace(brand) ? null : brand.ToLowerInvariant();
        StateHasChanged();
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnDefaultMethod(string methodId)
    {
        if (!string.IsNullOrWhiteSpace(methodId) &&
          methodId.Contains(":payfac:", StringComparison.OrdinalIgnoreCase))
        {
            SelectedPaymentMethod = methodId;
            StateHasChanged();
        }
        return Task.CompletedTask;
    }


    private static readonly Dictionary<string, string> _brandLogos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["visa"] = "/img/visa.png",
        ["mastercard"] = "/img/mastercard.png",
        ["amex"] = "/img/amex.png",
    };
    private static string? GetBrandLogo(string? brand)
    {
        if (!string.IsNullOrWhiteSpace(brand) && _brandLogos.TryGetValue(brand.Trim(), out var path))
            return path;
        return null;
    }
    public string BrandLogoSrc => GetBrandLogo(CardBrand) ?? string.Empty;
    public bool HasBrandLogo => !string.IsNullOrWhiteSpace(BrandLogoSrc);
    public string CardClass(string brand) =>
      string.Equals(CardBrand, brand, StringComparison.OrdinalIgnoreCase) ? "active" : "";
}