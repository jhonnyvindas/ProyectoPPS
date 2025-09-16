// PasarelaPago.Shared/Models/Pago.cs
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
    [Column(TypeName = "varchar(64)")]
    public string numeroOrden { get; set; } = default!;

    // FK a Clientes.cedula
    [Required, MaxLength(25)]
    [Column("cedula", TypeName = "varchar(25)")]
    public string cedula { get; set; } = default!;

    public Cliente? Cliente { get; set; }

    [Required, MaxLength(10)]
    [Column(TypeName = "varchar(10)")]
    public string metodoPago { get; set; } = default!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal monto { get; set; }

    [Required, MaxLength(3)]
    [Column(TypeName = "varchar(3)")]
    public string moneda { get; set; } = default!;

    [MaxLength(20)]
    [Column(TypeName = "varchar(20)")]
    public string? estadoTilopay { get; set; }

    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string? numeroAutorizacion { get; set; }

    [MaxLength(12)]
    [Column(TypeName = "varchar(12)")]
    public string? marcaTarjeta { get; set; }

    // nvarchar(MAX) para guardar el JSON completo de Tilopay
    [Column(TypeName = "nvarchar(max)")]
    public string? datosRespuestaTilopay { get; set; }

    [Column(TypeName = "datetime2(6)")]
    public DateTime fechaTransaccion { get; set; }
}
