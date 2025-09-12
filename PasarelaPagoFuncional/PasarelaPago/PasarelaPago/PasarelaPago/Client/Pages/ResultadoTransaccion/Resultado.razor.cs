using Microsoft.AspNetCore.Components;
using System;
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

        // Monto / moneda (pueden venir desde redirect o returnData)
        dict.TryGetValue("amount", out var amount);
        dict.TryGetValue("currency", out var currency);

        // Cliente: aceptamos varias claves (según lo que mandes en redirect/returnData)
        dict.TryGetValue("customer", out var customer);                 // "Nombre Apellidos"
        dict.TryGetValue("email", out var email);
        dict.TryGetValue("billToFirstName", out var firstName);
        dict.TryGetValue("billToLastName", out var lastName);

        // País y Cédula: aceptamos varias variantes
        dict.TryGetValue("billToCountry", out var country);
        if (string.IsNullOrWhiteSpace(country) && dict.TryGetValue("country", out var c2)) country = c2;

        dict.TryGetValue("customerId", out var idNumber);
        if (string.IsNullOrWhiteSpace(idNumber) && dict.TryGetValue("cedula", out var c3)) idNumber = c3;
        if (string.IsNullOrWhiteSpace(idNumber) && dict.TryGetValue("id", out var c4)) idNumber = c4;

        var displayCustomer = !string.IsNullOrWhiteSpace(customer)
            ? customer
            : $"{(firstName ?? "").Trim()} {(lastName ?? "").Trim()}".Trim();

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
            // nuevos
            Customer = customer,
            FirstName = firstName,
            LastName = lastName,
            DisplayCustomer = string.IsNullOrWhiteSpace(displayCustomer) ? null : displayCustomer,
            Country = country,
            IdNumber = idNumber,
            Email = email,
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
        if (string.IsNullOrWhiteSpace(b))
            return string.IsNullOrWhiteSpace(mask) ? "-" : mask;

        b = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(b.ToLowerInvariant()); // Visa, Mastercard, Amex
        return string.IsNullOrWhiteSpace(mask) ? b : $"{b}  —  {mask}";
    }


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

        // monto
        public string? Amount { get; set; }
        public string? Currency { get; set; }

     
        public string? Customer { get; set; }      // si viene ya concatenado
        public string? FirstName { get; set; }     // por si vienen separados
        public string? LastName { get; set; }
        public string? DisplayCustomer { get; set; } // nombre final a mostrar

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
