using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasarelaPago.Shared.Models
{
    public class Transaccion
    {
        public int id { get; set; }
        public string numeroOrden { get; set; }
        public string monto { get; set; }
        public string impuestos { get; set; }
        public string descuento { get; set; }
        public string nombreDescuento { get; set; }
        public string moneda { get; set; }
        public int idComerciante  { get; set; }
        public string codigoRespuesta { get; set; }
        public string respuesta { get; set; }
        public string autorizacion { get; set; }
        public string primerosDigitosTargeta { get; set; }
        public string utimosDigitosTargeta { get; set; }
        public string correoCliente { get; set; }
        public double comision { get; set; }
        public double comisionIVA { get; set; }
        public double retencionIVA { get; set; }
        public double retencionRenta { get; set; }
        public double costo { get; set; }
        public double costoIVA { get; set; }
        public double netoPagar { get; set; }
        public string  tipoCaptura{ get; set; }
        public string tipoTransaccion { get; set; }
        public string entorno { get; set; }
        public string fecha { get; set; }
    }
}
