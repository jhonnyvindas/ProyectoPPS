using Microsoft.AspNetCore.Mvc;
using PasarelaPago.Server.Services;
using PasarelaPago.Shared.Dtos;

namespace PasarelaPago.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransaccionController : ControllerBase
{
    private readonly TransaccionService _service;

    public TransaccionController(TransaccionService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] PagoConCliente dto)
    {
        if (dto is null || dto.Cliente is null || dto.Pago is null)
            return BadRequest("Payload inválido.");

        await _service.GuardarTransaccionAsync(dto.Cliente, dto.Pago);
        return Ok(new { ok = true });
    }
}
