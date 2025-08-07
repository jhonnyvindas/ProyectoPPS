using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasarelaPago.Shared.Models
{
    public class ConfiguracionTilopay
    {
        public string baseUrl { get; set; }
        public string llaveApi { get; set; }
        public string usuarioApi { get; set; }
        public string contrasenaApi { get; set; }
    }
}
