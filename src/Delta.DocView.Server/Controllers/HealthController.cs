using Delta.DocView.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Delta.DocView.Server.Controllers;

[AllowAnonymous]
[ApiController]
[Route("[controller]")]
public sealed class HealthController : ControllerBase
{
    private readonly IStartupError _error;
    private readonly IStepLibraryStore _store;

    public HealthController(IStartupError error, IStepLibraryStore store)
    {
        _error = error;
        _store = store;
    }

    [HttpGet("/health")]
    public IActionResult Get()
    {
        if (_store.IsLoaded)
            return Ok(new { status = "healthy" });

        return StatusCode(503, new
        {
            status = "unhealthy",
            reason = _error.ErrorMessage ?? "Library not loaded."
        });
    }
}
