using Delta.DocView.Server.Controllers;
using Delta.DocView.Server.Services;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Delta.DocView.Tests.Controllers;

public class HealthControllerTests
{
    private static object? Prop(object value, string name) =>
        value.GetType().GetProperty(name)!.GetValue(value);

    [Fact]
    public void Get_WhenLoaded_Returns200_HealthyStatus()
    {
        var error = Substitute.For<IStartupError>();
        var store = Substitute.For<IStepLibraryStore>();
        store.IsLoaded.Returns(true);
        var controller = new HealthController(error, store);

        var result = controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("healthy", Prop(ok.Value!, "status"));
    }

    [Fact]
    public void Get_WhenNotLoaded_Returns503_UnhealthyWithReason()
    {
        var error = Substitute.For<IStartupError>();
        error.ErrorMessage.Returns("boom");
        var store = Substitute.For<IStepLibraryStore>();
        store.IsLoaded.Returns(false);
        var controller = new HealthController(error, store);

        var result = controller.Get();

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, status.StatusCode);
        Assert.Equal("unhealthy", Prop(status.Value!, "status"));
        Assert.Equal("boom", Prop(status.Value!, "reason"));
    }

    [Fact]
    public void Get_WhenNotLoaded_NoErrorMessage_Returns503_DefaultReason()
    {
        var error = Substitute.For<IStartupError>();
        error.ErrorMessage.Returns((string?)null);
        var store = Substitute.For<IStepLibraryStore>();
        store.IsLoaded.Returns(false);
        var controller = new HealthController(error, store);

        var result = controller.Get();

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, status.StatusCode);
        Assert.Equal("unhealthy", Prop(status.Value!, "status"));
        Assert.Equal("Library not loaded.", Prop(status.Value!, "reason"));
    }
}
