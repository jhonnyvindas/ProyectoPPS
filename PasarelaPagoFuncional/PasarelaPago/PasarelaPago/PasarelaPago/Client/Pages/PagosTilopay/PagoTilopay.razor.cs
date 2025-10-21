using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Newtonsoft.Json.Linq;
using PasarelaPago.Client.Services;
using PasarelaPago.Shared.Dtos;
using PasarelaPago.Shared.Models;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.Intrinsics.X86;
using System.Text.Json;
using System.Threading.Tasks;

namespace PasarelaPago.Client.Pages.PagosTilopay;

public partial class PagoTilopay : ComponentBase, IAsyncDisposable
{
    [Inject] protected TilopayApi Api { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] protected HttpClient Http { get; set; } = default!;

    private readonly TimeSpan _minWaitTime = TimeSpan.FromSeconds(5);
    public string? SelectedPaymentMethod { get; set; } = null;
    public bool IsPayfac => (SelectedPaymentMethod?.Contains(":payfac:", StringComparison.OrdinalIgnoreCase) ?? false);
    public string? CardNumber { get; set; } = "4012000000020089";
    public string? Expiry { get; set; } = "12/26";
    public string? CVV { get; set; } = "123";
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

    public decimal Amount { get; set; } = 10000m;
    public string Currency { get; set; } = "CRC";
    public string AmountString => Amount.ToString("F2", CultureInfo.InvariantCulture);

    public string RedirectUrl => BuildRedirectUrl("/pagos/resultado");
    public bool Pagando { get; set; }
    public string? Estado { get; set; }

    private DotNetObjectReference<PagoTilopay>? _selfRef;

    private bool ShowFailModal { get; set; } = false;
    private string? FailReason { get; set; }

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
        //_selfRef = DotNetObjectReference.Create(this);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _selfRef = DotNetObjectReference.Create(this);
        }

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
        var scheme = uri.Scheme;                     
        var host = uri.Host;
        var port = uri.IsDefaultPort ? "" : $":{uri.Port}";
        var baseUrl = $"{scheme}://{host}{port}{path}";
        if (query is null || query.Count == 0) return baseUrl;

        var parts = new List<string>(query.Count);
        foreach (var kv in query)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || string.IsNullOrWhiteSpace(kv.Value)) continue;
            parts.Add($"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}");
        }
        return parts.Count > 0 ? $"{baseUrl}?{string.Join("&", parts)}" : baseUrl;
    }

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
    private Task OnFailOk()
    {
        ShowFailModal = false;   
        FailReason = null;
        Pagando = false;
        Estado = null;

        StateHasChanged();
        return Task.CompletedTask;
    }

    private sealed class PrepararOrdenResponse
    {
        public string Token { get; set; } = default!;
        public DateTime ExpiraUtc { get; set; }
    }

    private string? _redirectUrlForPrep;

    public async Task Pagar()
    {
        if (Pagando) return;

        _paymentStartTime = DateTime.UtcNow;
        Pagando = true;
        ShowFailModal = false;

        try
        {
            if (string.IsNullOrWhiteSpace(CustomerId))
            {
                Estado = "Debe indicar la cédula del cliente.";
                Pagando = false; StateHasChanged();
                return;
            }

            Estado = "Obteniendo token…";
            StateHasChanged();

            var sdkToken = await Api.GetSdkTokenAsync();
            if (string.IsNullOrWhiteSpace(sdkToken))
            {
                Estado = "No se obtuvo token del servidor.";
                Pagando = false; StateHasChanged();
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedPaymentMethod) || !IsPayfac)
            {
                try
                {
                    var payfac = await JS.InvokeAsync<string>("tilopayInterop.getPayfacMethod");
                    if (!string.IsNullOrWhiteSpace(payfac)) SelectedPaymentMethod = payfac;
                }
                catch { }
            }
            if (string.IsNullOrWhiteSpace(SelectedPaymentMethod) || !IsPayfac)
            {
                Estado = "No hay método de tarjeta disponible para la moneda seleccionada.";
                Pagando = false; StateHasChanged();
                return;
            }

            _orderNumber = Guid.NewGuid().ToString("N");
            StateHasChanged();

            var preparar = new
            {
                NumeroOrden = _orderNumber,
                Cedula = CustomerId!,
                Monto = Amount,
                Moneda = Currency,
                Nombre = BillToFirstName,
                Apellido = BillToLastName,
                Email = BillToEmail,
                Pais = BillToCountry
            };

            var prepResp = await Http.PostAsJsonAsync("api/Transaccion/preparar-orden", preparar);
            prepResp.EnsureSuccessStatusCode();
            var prep = await prepResp.Content.ReadFromJsonAsync<PrepararOrdenResponse>();
            if (prep is null || string.IsNullOrWhiteSpace(prep.Token))
            {
                Estado = "No fue posible preparar la orden.";
                Pagando = false; StateHasChanged();
                return;
            }

            _redirectUrlForPrep = $"/api/Transaccion/callback/{prep.Token}";

            var redirectUrl = BuildRedirectUrl(_redirectUrlForPrep);

            Estado = "Inicializando SDK…";
            StateHasChanged();

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

            await JS.InvokeVoidAsync("tilopayInterop.ensureInit", sdkToken, options, _selfRef);

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

            await JS.InvokeVoidAsync("tilopayInterop.prepareAndPayWithTimeout", 60000);
        }
        catch (JSException jse)
        {
            Estado = $"Error JS/SDK: {jse.Message}";
            ShowFailure("Ocurrió un problema con el SDK. Intente nuevamente.");
            Pagando = false; StateHasChanged();
        }
        catch (HttpRequestException hre)
        {
            Estado = $"Error de red/HTTP: {hre.Message}";
            ShowFailure("No se pudo comunicar con el servidor. Intente nuevamente.");
            Pagando = false; StateHasChanged();
        }
        catch (Exception ex)
        {
            Estado = $"Error: {ex.Message}";
            ShowFailure("Ocurrió un problema inesperado. Intente nuevamente.");
            Pagando = false; StateHasChanged();
        }
    }

    public sealed class PaymentEvent
    {
        public string? status { get; set; }
        public object? payload { get; set; }
    }
    private static bool EsValidacionInline(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return false;

        if (payload.Contains("\"inlineError\"", StringComparison.OrdinalIgnoreCase)) return true;

        return
            payload.Contains("cvv", StringComparison.OrdinalIgnoreCase) ||
            payload.Contains("cvc", StringComparison.OrdinalIgnoreCase) ||
            payload.Contains("número de tarjeta", StringComparison.OrdinalIgnoreCase) ||
            payload.Contains("numero de tarjeta", StringComparison.OrdinalIgnoreCase) ||
            payload.Contains("tarjeta vencida", StringComparison.OrdinalIgnoreCase) ||
            payload.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
            payload.Contains("invalido", StringComparison.OrdinalIgnoreCase) ||
            payload.Contains("inválido", StringComparison.OrdinalIgnoreCase);
    }

    [JSInvokable]
    public async Task OnPaymentEvent(PaymentEvent evt)
    {
        var status = (evt?.status ?? "").ToLowerInvariant();
        var payload = evt?.payload?.ToString() ?? "";

        Console.WriteLine($"OnPaymentEvent: status={status}, payload={payload}");

        if (EsValidacionInline(payload))
        {
            try { await JS.InvokeVoidAsync("tilopayInterop.maybeCancel"); } catch { }
            Estado = "Revise los datos de la tarjeta.";
            await AplicarRetardoUX();
            ShowFailure("CVV/fecha/número inválido. Corrija e intente de nuevo.");
            Pagando = false;
            StateHasChanged();
            return;
        }

        if (status == "approved" || status == "rejected")
        {
            try { await JS.InvokeVoidAsync("tilopayInterop.maybeCancel"); } catch { }
            if (!string.IsNullOrWhiteSpace(_redirectUrlForPrep))
                Nav.NavigateTo(_redirectUrlForPrep!, forceLoad: true);
            return;
        }

        if (status is "timeout" or "failed" or "error" or "cancelled" or "canceled" or "void")
        {
            Estado = "No fue posible completar la transacción.";
            await AplicarRetardoUX();
            ShowFailure("Ocurrió un problema. Intente nuevamente.");
            Pagando = false;
            StateHasChanged();
            return;
        }

        var trimmed = (payload ?? "").Trim();
        bool emptyPayload = string.IsNullOrEmpty(trimmed) || trimmed == "{\"message\":\"\"}" || trimmed == "{}";
        if (emptyPayload)
        {
            return;
        }

        try { await JS.InvokeVoidAsync("tilopayInterop.maybeCancel"); } catch { }
        if (!string.IsNullOrWhiteSpace(_redirectUrlForPrep))
            Nav.NavigateTo(_redirectUrlForPrep!, forceLoad: true);
    }

    private async Task AplicarRetardoUX()
    {
        if (_paymentStartTime.HasValue)
        {
            var elapsed = DateTime.UtcNow - _paymentStartTime.Value;
            var remaining = _minWaitTime - elapsed;
            if (remaining.TotalMilliseconds > 0)
                await Task.Delay(remaining);
        }
    }

    [JSInvokable]
    public async Task OnPaymentTimeout()
    {
        Pagando = false;

        if (_paymentStartTime.HasValue)
        {
            var elapsed = DateTime.UtcNow - _paymentStartTime.Value;
            var remainingDelay = _minWaitTime - elapsed;

            if (remainingDelay.TotalMilliseconds > 0)
            {
                await Task.Delay(remainingDelay);
            }
        }

        Estado = "Tiempo de espera agotado.";
        ShowFailure("Tiempo de espera agotado. No fue posible completar la transacción.");

        StateHasChanged();
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