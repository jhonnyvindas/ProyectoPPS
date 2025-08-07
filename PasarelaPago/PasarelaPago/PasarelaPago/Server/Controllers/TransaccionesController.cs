using Microsoft.AspNetCore.Mvc;
using PasarelaPago.Shared.Models;

namespace PasarelaPago.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TransaccionesController : ControllerBase
    {
        private readonly TilopayService _tilopayService;

        public TransaccionesController(TilopayService tilopayService)
        {
            _tilopayService = tilopayService;
        }

        [HttpGet]
        public async Task<ActionResult<List<Transaccion>>> Obtener()
        {
            try
            {
                var transacciones = await _tilopayService.ObtenerTransaccionesAsync();
                return Ok(transacciones);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al consultar transacciones: {ex.Message}");
            }
        }
    }
}
