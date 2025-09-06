using PasarelaPago.Shared.Models;

namespace PasarelaPago.Shared.Dtos;

public class PagoConCliente
{
    public Cliente Cliente { get; set; }
    public Pago Pago { get; set; }
}