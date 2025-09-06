using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PasarelaPago.Shared.Models;

[Table("Pagos")]
public class Pago
{
    [Key]
    public int pagoId { get; set; }
    public string? numeroOrden { get; set; }
    public int? clienteId { get; set; }
    public string? metodoPago { get; set; }
    public decimal? monto { get; set; }
    public string? moneda { get; set; }
    public string? estadoTilopay { get; set; }
    public string? datosRespuestaTilopay { get; set; }
    public DateTime? fechaTransaccion { get; set; }
    public string? marcaTarjeta { get; set; }
}