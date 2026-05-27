using Delta.DocView.Server.Services;
using Delta.DocView.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Delta.DocView.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LibraryController : ControllerBase
{
    private readonly IStartupError _error;
    private readonly IStepLibraryStore _store;

    public LibraryController(IStartupError error, IStepLibraryStore store)
    {
        _error = error;
        _store = store;
    }

    [HttpGet]
    public IActionResult Get()
    {
        if (_error.HasError || !_store.IsLoaded)
            return StatusCode(503, new { error = _error.ErrorMessage ?? "Library not loaded." });

        var response = new LibraryResponse(
            _store.Library!,
            _error.HasWarning ? _error.WarningMessage : null);

        return Ok(response);
    }
}
