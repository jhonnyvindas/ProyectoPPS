using Microsoft.AspNetCore.Mvc;

using PasarelaPago.Server.Services;

using PasarelaPago.Shared.Models;



namespace PasarelaPago.Server.Controllers;



[ApiController]

[Route("api/[controller]")]

public class TransaccionController : ControllerBase

{

    private readonly TransaccionService _svc;

    public TransaccionController(TransaccionService svc) => _svc = svc;



    public class PagoConCliente

    {

        public Cliente Cliente { get; set; } = default!;

        public Pago Pago { get; set; } = default!;

    }



    [HttpPost]

    public async Task<IActionResult> Post([FromBody] PagoConCliente body)

    {

        if (body is null) return BadRequest("Cuerpo vacío.");



        // Garantiza cédula presente en ambos objetos

        var ced = body.Cliente?.cedula ?? body.Pago?.cedula;

        if (string.IsNullOrWhiteSpace(ced))

            return BadRequest("La cédula es requerida.");



        body.Cliente ??= new Cliente { cedula = ced };

        body.Cliente.cedula = ced;

        body.Pago.cedula = ced;



        await _svc.GuardarTransaccionAsync(body.Cliente, body.Pago);

        return Ok();

    }

}