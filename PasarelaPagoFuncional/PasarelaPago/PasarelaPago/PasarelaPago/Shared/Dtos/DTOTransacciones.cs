using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasarelaPago.Shared.Dtos
{
    public class DTOTransacciones
    {
        public string cedula { get; set; }
        public string nombreCliente { get; set; }
        public string pais { get; set; }
        public decimal monto { get; set; }
        public string moneda { get; set; }
        public string numeroOrden { get; set; }
        public string estadoTransaccion { get; set; }
        public DateTime? fechaTransaccion { get; set; }
    }

    public class FiltroTransacciones
    {
        public int Pagina { get; set; } = 1;
        public int Tamanio { get; set; } = 20;
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string? EstadoTransaccion { get; set; }
        public string? Busqueda { get; set; }
    }

    public class PaginacionResponse<T>
    {
        public int TotalRegistros { get; set; }
        public List<T> Resultados { get; set; } = new();
    }
}
