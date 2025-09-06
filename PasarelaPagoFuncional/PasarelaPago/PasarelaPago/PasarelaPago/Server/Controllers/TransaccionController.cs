using Microsoft.AspNetCore.Mvc;
using PasarelaPago.Shared.Dtos; 
using PasarelaPago.Server.Services; 

[ApiController]
[Route("[controller]")]
public class TransaccionController : ControllerBase
{
    private readonly TransaccionService _transaccionService;

    public TransaccionController(TransaccionService transaccionService)
    {
        _transaccionService = transaccionService;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] PagoConCliente pagoConCliente)
    {
        await _transaccionService.GuardarTransaccionAsync(pagoConCliente.Cliente, pagoConCliente.Pago);
        return Ok();
    }
}