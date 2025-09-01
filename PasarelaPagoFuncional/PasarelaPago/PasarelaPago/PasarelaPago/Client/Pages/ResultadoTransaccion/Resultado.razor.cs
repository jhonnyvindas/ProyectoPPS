using Microsoft.AspNetCore.Components;
using System.Globalization;
using System.Web;

namespace PasarelaPago.Client.Pages.ResultadoTransaccion;

public partial class Resultado : ComponentBase
{
    [Inject] private NavigationManager Nav { get; set; } = default!;

    public bool Loading { get; private set; } = true;
    public PaymentResult Result { get; private set; } = new();

    // Banner UI
    protected string BannerCss { get; private set; } = "banner-success";
    protected string BannerIcon { get; private set; } = MudBlazor.Icons.Material.Filled.CheckCircle;
    protected string BannerText { get; private set; } = "¡Pago aprobado!";

    protected override void OnInitialized()
    {
        ParseFromUrl();
        Loading = false;
    }

    private void ParseFromUrl()
    {
        var uri = new Uri(Nav.Uri);
        var dict = ParseQuery(uri.Query);

        // Campos que Tilopay suele enviar
        dict.TryGetValue("code", out var code);
        dict.TryGetValue("description", out var description);
        dict.TryGetValue("auth", out var auth);
        dict.TryGetValue("order", out var order);
        dict.TryGetValue("brand", out var brand);
        dict.TryGetValue("last-digits", out var last4);
        dict.TryGetValue("tilopay-transaction", out var tilopayTx);
        if (string.IsNullOrWhiteSpace(tilopayTx) && dict.TryGetValue("tpt", out var tpt))
            tilopayTx = tpt;

        // Opcionales (por si los incluyes en redirect o en returnData)
        dict.TryGetValue("amount", out var amount);
        dict.TryGetValue("currency", out var currency);
        dict.TryGetValue("customer", out var customer);
        dict.TryGetValue("email", out var email);

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
            Email = email
        };

        // Banner
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
        if (string.IsNullOrWhiteSpace(b)) return string.IsNullOrWhiteSpace(mask) ? "-" : mask;
        b = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(b.ToLowerInvariant()); // Visa, Mastercard, Amex
        return string.IsNullOrWhiteSpace(mask) ? b : $"{b}  —  {mask}";
    }

    public sealed class PaymentResult
    {
        public string? Code { get; set; }
        public string? Description { get; set; }
        public string? Auth { get; set; }
        public string? Order { get; set; }
        public string? Brand { get; set; }
        public string? Last4 { get; set; }
        public string? TilopayTx { get; set; }

        public string? Amount { get; set; }
        public string? Currency { get; set; }
        public string? Customer { get; set; }
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
