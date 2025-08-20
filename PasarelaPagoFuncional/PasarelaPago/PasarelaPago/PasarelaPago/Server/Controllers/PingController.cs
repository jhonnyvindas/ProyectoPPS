using Microsoft.AspNetCore.Mvc;

namespace PasarelaPago.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PingController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("pong");
}
