using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PasarelaPago.Client.Services;
using PasarelaPago.Shared.Dtos; // <-- NUEVO
using PasarelaPago.Shared.Models;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Linq;
using System.Net.Http.Json; // <-- NUEVO

namespace PasarelaPago.Client.Pages.PagosTilopay;

public partial class PagoTilopay : ComponentBase, IAsyncDisposable
{
    // -------- Inyecciones --------
    [Inject] protected TilopayApi Api { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] protected HttpClient Http { get; set; } = default!; // <-- CAMBIO

    // -------- Constantes Tilopay (métodos) --------
    public const string PAYFAC = "12:3:88802749:payfac:0:";
    public const string SIMPE = "12:3:88802749:simpe:0:";

    // -------- Estado UI / Binding (coinciden con el .razor) --------
    public string SelectedPaymentMethod { get; set; } = PAYFAC;

    // Tarjeta
    public string? CardNumber { get; set; } = "4012000000020089";
    public string? Expiry { get; set; } = "12/26";
    public string? CVV { get; set; } = "123";

    // SINPE
    public string? Phone { get; set; }
    public decimal? SinpeAmount { get; set; }
    public string? SinpeRef { get; set; }

    // Propietario (visibles en el formulario)
    public bool OwnerReadOnly => false;
    public string? BillToFirstName { get; set; } = "Demo";
    public string? BillToLastName { get; set; } = "User";
    public string? CustomerId { get; set; }
    public string? BillToTelephone { get; set; }
    public string? BillToEmail { get; set; } = "demo@example.com";
    public string? BillToCountry { get; set; } = "CR";
    public string? BillToZipPostCode { get; set; }
    public string? BillToCity { get; set; } = "San José";
    public string? BillToState { get; set; } = "SJ";
    public string? BillToAddress { get; set; } = "123 Main Street";

    // Marca de tarjeta (desde el SDK)
    public string? CardBrand { get; set; }
    public int CvvMaxLen => CardBrand == "amex" ? 4 : 3;
    public int CardNumberMaxLen => CardBrand == "amex" ? 15 : 19;

    public int CardNumberMaxChars => CardBrand == "amex" ? 17 : 22;

    // Validación lógica por dígitos (sin espacios)
    public int CardNumberDigitsMax => CardBrand == "amex" ? 15 : 19;

    public string? ValidateCardNumber(string? v)
    {
        var digits = new string((v ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length < 13 || digits.Length > CardNumberDigitsMax)
            return $"Número de tarjeta inválido ({digits.Length}/{CardNumberDigitsMax})";
        return null;
    }

    // Base del pago
    private string _orderNumber = Guid.NewGuid().ToString("N");
    public string OrderNumber => _orderNumber;

    public decimal Amount { get; set; } = 1.00m;
    public string Currency { get; set; } = "USD";
    public string AmountString => Amount.ToString("F2", CultureInfo.InvariantCulture);
    private bool _sdkReady;

    // construye URL absoluta (Tilopay suele requerir https)
    public string RedirectUrl => BuildRedirectUrl("/pagos/resultado");

    // Estado y callback
    public bool Pagando { get; set; }
    public string? Estado { get; set; }

    private DotNetObjectReference<PagoTilopay>? _selfRef;

    // -------- Helpers de UI (labels, atributos) --------
    public string PayLabel => FormatPayLabel(
        SelectedPaymentMethod == PAYFAC ? Amount : (SinpeAmount ?? Amount),
        Currency
    );

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

    // InputAttributes usados por el .razor
    public IReadOnlyDictionary<string, object> PhoneInputAttrs => new Dictionary<string, object>
    {
        ["id"] = "sinpe_phone",
        ["name"] = "sinpe_phone",
        ["inputmode"] = "numeric",
        ["maxlength"] = "9"
    };
    public IReadOnlyDictionary<string, object> CustomerIdInputAttrs => new Dictionary<string, object>
    {
        ["id"] = "customerId",
        ["name"] = "customerId",
        ["inputmode"] = "numeric",
        ["maxlength"] = "12"
    };
    public IReadOnlyDictionary<string, object> BillToTelephoneInputAttrs => new Dictionary<string, object>
    {
        ["id"] = "billToTelephone",
        ["name"] = "billToTelephone",
        ["inputmode"] = "numeric",
        ["maxlength"] = "9"
    };
    public IReadOnlyDictionary<string, object> CountryAttrs => new Dictionary<string, object>
    {
        ["id"] = "billToCountry",
        ["name"] = "billToCountry"
    };
    public IReadOnlyDictionary<string, object> ZipAttrs => new Dictionary<string, object>
    {
        ["id"] = "billToZipPostCode",
        ["name"] = "billToZipPostCode",
        ["inputmode"] = "numeric"
    };
    public IReadOnlyDictionary<string, object> CityAttrs => new Dictionary<string, object>
    {
        ["id"] = "billToCity",
        ["name"] = "billToCity"
    };
    public IReadOnlyDictionary<string, object> StateAttrs => new Dictionary<string, object>
    {
        ["id"] = "billToState",
        ["name"] = "billToState"
    };
    public IReadOnlyDictionary<string, object> Address1Attrs => new Dictionary<string, object>
    {
        ["id"] = "billToAddress",
        ["name"] = "billToAddress"
    };

    // -------- Ciclo de vida --------
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
            // 1) Obtener token y pre-inicializar el SDK
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
                    redirect = RedirectUrl
                };

                await JS.InvokeVoidAsync("tilopayInterop.ensureInit", token, preOptions, _selfRef);
                _sdkReady = true;
            }
        }
        catch { /* si falla, sólo no habrá detección de marca */ }

        // 2) Enganchar detector de marca (ahora el SDK ya está listo)
        await JS.InvokeVoidAsync("tilopayInterop.watchCardBrand", _selfRef);
    }


    public async ValueTask DisposeAsync()
    {
        _selfRef?.Dispose();
        await Task.CompletedTask;
    }

    private string BuildRedirectUrl(string path)
    {
        var uri = new Uri(Nav.Uri);
        var scheme = "https"; // fuerza https en dev si estás en http
        var host = uri.Host;
        var port = uri.IsDefaultPort ? "" : $":{uri.Port}";
        return $"{scheme}://{host}{port}{path}";
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

    private string ToInvariantAmount(decimal amount)
        => amount.ToString("F2", CultureInfo.InvariantCulture);

    // -------- Pago --------
    public async Task Pagar()
    {
        if (Pagando) return;
        Pagando = true;

        try
        {
            Estado = "Obteniendo token…";
            StateHasChanged();

            var token = await Api.GetSdkTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                Estado = "No se obtuvo token del servidor.";
                return;
            }

            var amountToPay = SelectedPaymentMethod == PAYFAC
                ? Amount
                : (SinpeAmount ?? 0m);

            if (SelectedPaymentMethod == SIMPE && amountToPay <= 0m)
            {
                Estado = "Monto SINPE inválido.";
                return;
            }

            _orderNumber = Guid.NewGuid().ToString("N");
            StateHasChanged();

            Estado = "Inicializando SDK…";
            StateHasChanged();

            var options = new
            {
                orderNumber = _orderNumber,
                amount = ToInvariantAmount(amountToPay),
                currency = Currency,
                description = "Pago de servicios",
                language = "es",
                capture = "1",
                redirect = RedirectUrl,
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
            catch { /* no-op */ }

            Estado = "Invocando checkout…";
            StateHasChanged();

            await JS.InvokeVoidAsync("tilopayInterop.prepareAndPay");

            Estado = "Esperando confirmación del banco…";
        }
        catch (JSException jse)
        {
            Estado = $"Error JS/SDK: {jse.Message}";
        }
        catch (Exception ex)
        {
            Estado = $"Error: {ex.Message}";
        }
        finally
        {
            Pagando = false;
            StateHasChanged();
        }
    }


[JSInvokable]
public async Task OnPaymentEvent(PaymentEvent evt)
{
    var status = (evt?.status ?? "").ToLowerInvariant();

    if (status == "success" || status == "approved" || status == "ok" || status == "completed")
    {
        // 1) Calcular monto SIEMPRE no-null
        decimal amountToPay = (SelectedPaymentMethod == PAYFAC)
            ? Amount                         // Amount ya es decimal no-null
            : (SinpeAmount ?? 0m);           // SINPE podría venir null

        if (amountToPay <= 0m)
        {
            Estado = "Monto inválido. No se puede registrar el pago.";
            StateHasChanged();
            return;
        }

        // 2) Construir Cliente
        var cliente = new Cliente
        {
            nombre = BillToFirstName,
            apellido = BillToLastName,
            correo = BillToEmail,
            telefono = BillToTelephone,
            direccion = BillToAddress,
            ciudad = BillToCity,
            provincia = BillToState,
            codigoPostal = BillToZipPostCode,
            pais = BillToCountry
        };

        // 3) Serializar payload del SDK en texto (evita .ToString() [object])
        string? payloadTexto = null;
        try { payloadTexto = JsonSerializer.Serialize(evt?.payload); }
        catch { payloadTexto = evt?.payload?.ToString(); }

        // 4) Construir Pago (campos obligatorios NO nulos)
        var pago = new Pago
        {
            numeroOrden = _orderNumber,
            metodoPago = SelectedPaymentMethod,
            monto = amountToPay,                  // <--- decimal no-null
            moneda = Currency ?? "USD",
            estadoTilopay = status,
            datosRespuestaTilopay = payloadTexto,
            fechaTransaccion = DateTime.UtcNow,              // <--- no-null
            marcaTarjeta = (CardBrand ?? "").ToLowerInvariant()
        };

        var payload = new PagoConCliente { Cliente = cliente, Pago = pago };

        try
        {
            // 5) Llamar API correcta
            var resp = await Http.PostAsJsonAsync("api/Transaccion", payload);
            if (resp.IsSuccessStatusCode)
                Estado = "Pago aprobado. Datos guardados.";
            else
                Estado = $"Error al guardar datos: {(int)resp.StatusCode} {resp.ReasonPhrase}";
        }
        catch (Exception ex)
        {
            Estado = $"Error al guardar datos: {ex.Message}";
        }
    }
    else
    {
        Estado = $"Pago rechazado: {evt?.payload}";
    }

    StateHasChanged();
}


[JSInvokable]
    public Task OnCardBrandChanged(string brand)
    {
        CardBrand = (brand ?? "").ToLowerInvariant();
        StateHasChanged();
        return Task.CompletedTask;
    }



    // ... Resto de tu código ...

    public sealed class PaymentEvent
    {
        public string? status { get; set; }
        public object? payload { get; set; }
    }

    public string CardClass(string brand) =>
        string.Equals(CardBrand, brand, StringComparison.OrdinalIgnoreCase) ? "active" : "";
}