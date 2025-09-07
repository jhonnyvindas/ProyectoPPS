using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PasarelaPago.Shared.Models;

[Table("Pagos")]
public class Pago
{
    [Key]
    public int pagoId { get; set; }

    [Required, MaxLength(64)]
    public string numeroOrden { get; set; } = default!;

    [Required]
    public int clienteId { get; set; }

    [MaxLength(32)]
    public string? metodoPago { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal monto { get; set; }

    [MaxLength(5)]
    public string? moneda { get; set; }

    [MaxLength(50)]
    public string? estadoTilopay { get; set; }

    public string? datosRespuestaTilopay { get; set; }

    public DateTime fechaTransaccion { get; set; }

    [MaxLength(16)]
    public string? marcaTarjeta { get; set; }
}
