using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.JSInterop;
using PasarelaPago.Client;
using System;

namespace PasarelaPago.Client.Pages.PagosTilopay;

public partial class PagoTilopay : ComponentBase, IAsyncDisposable
{

    [Inject] private PasarelaPago.Client.Services.TilopayApi Api { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    private IJSObjectReference? _watcher;

    private string? Estado;
    private bool Pagando;
    private DotNetObjectReference<PagoTilopay>? _selfRef;

    private string OrderNumber { get; set; } = Guid.NewGuid().ToString("N");
    private decimal Amount { get; set; } = 1.00m;
    private string AmountString => Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
    private string Currency { get; set; } = "USD";
    private string RedirectUrl => BuildRedirectUrl("/pagos/resultado");
    private string CardBrand { get; set; } = "";

    protected override void OnInitialized()
    {
        _selfRef = DotNetObjectReference.Create(this);
    }
    private string BuildRedirectUrl(string path)
    {
        var uri = new Uri(Nav.Uri);
        var scheme = "https"; // conservado tal cual tu versión
        var host = uri.Host;
        var port = uri.IsDefaultPort ? "" : $":{uri.Port}";
        return $"{scheme}://{host}{port}{path}";
    }

    private async Task Pagar()
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

            Estado = "Inicializando SDK…";
            StateHasChanged();

            OrderNumber = Guid.NewGuid().ToString("N");
            StateHasChanged();

            await JS.InvokeVoidAsync("tilopayInterop.ensureInit", token, new
            {
                orderNumber = OrderNumber,
                amount = AmountString,
                currency = Currency,
                description = "Compra de prueba",
                language = "es",
                capture = "1",
                redirect = RedirectUrl,
                billToEmail = "demo@example.com",
                billToFirstName = "Demo",
                billToLastName = "User",
                billToAddress = "123 Main Street",
                billToCity = "San José",
                billToCountry = "CR",
                billToState = "SJ",
                billToZipPostCode = "10101",
                billToTelephone = "88888888",
                subscription = 0,
                hashVersion = "V2"
            }, _selfRef!);

            Estado = "Validando datos…";
            StateHasChanged();

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
    public Task OnPaymentEvent(PaymentEvent evt)
    {
        var status = (evt?.status ?? "").ToLowerInvariant();
        Estado = status switch
        {
            "success" or "approved" or "ok" or "completed" => " Pago aprobado",
            "error" or "failed" or "denied" or "declined" => $" Pago rechazado: {evt?.payload}",
            _ => $" Resultado: {evt?.status} {evt?.payload}"
        };

        StateHasChanged();
        return Task.CompletedTask;
    }

    public sealed class PaymentEvent
    {
        public string? status { get; set; }
        public object? payload { get; set; }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await JS.InvokeVoidAsync(
            "tilopayInterop.watchCardType",
            "#tlpy_cc_number",
            _selfRef,
            nameof(SetCardBrand)
        );
    }

    public async ValueTask DisposeAsync()
    {
        // ya no intentes llamar a _watcher.InvokeVoidAsync("call")
        _selfRef?.Dispose();
        await Task.CompletedTask;
    }


    [JSInvokable]
    public Task SetCardBrand(string brand)
    {
        CardBrand = brand ?? "";
        StateHasChanged();
        return Task.CompletedTask;
    }
}

