// PasarelaPago.Shared/Models/Cliente.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PasarelaPago.Shared.Models;

[Table("Clientes")]
public class Cliente
{
    [Key]
    [Required, MaxLength(25)]
    [Column("cedula", TypeName = "varchar(25)")]
    public string cedula { get; set; } = default!;

    [MaxLength(50)][Column(TypeName = "nvarchar(50)")] public string? nombre { get; set; }
    [MaxLength(50)][Column(TypeName = "nvarchar(50)")] public string? apellido { get; set; }
    [MaxLength(100)][EmailAddress][Column(TypeName = "nvarchar(100)")] public string? correo { get; set; }
    [MaxLength(20)][Column(TypeName = "nvarchar(20)")] public string? telefono { get; set; }
    [MaxLength(100)][Column(TypeName = "nvarchar(100)")] public string? direccion { get; set; }
    [MaxLength(50)][Column(TypeName = "nvarchar(50)")] public string? ciudad { get; set; }
    [MaxLength(50)][Column(TypeName = "nvarchar(50)")] public string? provincia { get; set; }
    [MaxLength(20)][Column(TypeName = "nvarchar(20)")] public string? codigoPostal { get; set; }
    [MaxLength(10)][Column(TypeName = "nvarchar(10)")] public string? pais { get; set; }

    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}
