using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PasarelaPago.Shared.Models
{
    [Table("Clientes")]
    public class Cliente
    {
        [Key] public int clienteId { get; set; }

        [MaxLength(50)] public string? nombre { get; set; }
        [MaxLength(50)] public string? apellido { get; set; }
        [EmailAddress, MaxLength(100)] public string? correo { get; set; }
        [MaxLength(20)] public string? telefono { get; set; }
        [MaxLength(100)] public string? direccion { get; set; }
        [MaxLength(50)] public string? ciudad { get; set; }
        [MaxLength(50)] public string? provincia { get; set; }
        [MaxLength(20)] public string? codigoPostal { get; set; }
        [MaxLength(10)] public string? pais { get; set; }

        public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    }
}
