using Delta.DocView.Server.Controllers;
using Delta.DocView.Server.Services;
using Delta.DocView.Shared;
using Delta.DocView.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Delta.DocView.Tests.Controllers;

public class LibraryControllerTests
{
    private static readonly StepLibrary SampleLibrary = new()
    {
        Version = "1.0.0",
        GeneratedAt = "2026-01-01T00:00:00Z",
        GeneratorVersion = "1.0.0",
        Domains = [new StepDomain { Id = "Auth", Label = "Auth & Identity" }],
        Steps =
        [
            new Step
            {
                Id = "auth-001a2b3c", Type = "Given",
                Pattern = "I am logged in as {string}",
                Params = [], File = "Auth/AuthSteps.cs", Line = 10,
                Domain = "Auth", Tags = [], Used = 1,
                Description = "", Source = "", SuggestsNext = []
            }
        ],
        Signature = new StepSignature { Algorithm = "SHA-256", Digest = new string('0', 64) }
    };

    [Fact]
    public void Get_HasError_Returns503WithErrorMessage()
    {
        var error = Substitute.For<IStartupError>();
        error.HasError.Returns(true);
        error.ErrorMessage.Returns("boom");
        var store = Substitute.For<IStepLibraryStore>();
        var controller = new LibraryController(error, store);

        var result = controller.Get();

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, status.StatusCode);
    }

    [Fact]
    public void Get_NotLoadedNoError_Returns503()
    {
        var error = Substitute.For<IStartupError>();
        error.HasError.Returns(false);
        var store = Substitute.For<IStepLibraryStore>();
        store.IsLoaded.Returns(false);
        var controller = new LibraryController(error, store);

        var result = controller.Get();

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, status.StatusCode);
    }

    [Fact]
    public void Get_Loaded_Returns200WithLibrary()
    {
        var error = Substitute.For<IStartupError>();
        var store = Substitute.For<IStepLibraryStore>();
        store.IsLoaded.Returns(true);
        store.Library.Returns(SampleLibrary);
        var controller = new LibraryController(error, store);

        var result = controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<LibraryResponse>(ok.Value);
        Assert.Same(SampleLibrary, body.Library);
        Assert.Null(body.Warning);
    }

    [Fact]
    public void Get_LoadedWithWarning_Returns200WithWarning()
    {
        var error = Substitute.For<IStartupError>();
        error.HasWarning.Returns(true);
        error.WarningMessage.Returns("signature mismatch");
        var store = Substitute.For<IStepLibraryStore>();
        store.IsLoaded.Returns(true);
        store.Library.Returns(SampleLibrary);
        var controller = new LibraryController(error, store);

        var result = controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<LibraryResponse>(ok.Value);
        Assert.Equal("signature mismatch", body.Warning);
    }
}
