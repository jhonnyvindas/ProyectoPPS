using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PasarelaPago.Shared.Models;

[Table("Clientes")]
public class Cliente
{
    [Key]
    public int clienteId { get; set; }
    public string? nombre { get; set; }
    public string? apellido { get; set; }
    public string? correo { get; set; }
    public string? telefono { get; set; }
    public string? direccion { get; set; }
    public string? ciudad { get; set; }
    public string? provincia { get; set; }
    public string? codigoPostal { get; set; }
    public string? pais { get; set; }
}