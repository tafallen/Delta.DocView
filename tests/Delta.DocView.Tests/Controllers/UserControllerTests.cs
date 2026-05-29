using System.Security.Claims;
using Delta.DocView.Server.Auth;
using Delta.DocView.Server.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Delta.DocView.Tests.Controllers;

public class UserControllerTests
{
    private static object? Prop(object value, string name) =>
        value.GetType().GetProperty(name)!.GetValue(value);

    private static UserController MakeController(string name, string authType)
    {
        var controller = new UserController();
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, name)], authType);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
        return controller;
    }

    [Fact]
    public void GetUser_Returns200_WithNameAndInitials()
    {
        var controller = MakeController("Ada Lovelace", AuthConstants.DevBypassScheme);

        var result = controller.GetUser();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Ada Lovelace", Prop(ok.Value!, "name"));
        Assert.Equal("AL", Prop(ok.Value!, "initials"));
        Assert.Equal(true, Prop(ok.Value!, "authenticated"));
    }

    [Fact]
    public void GetUser_SingleClaimName_ReturnsCorrectInitials()
    {
        var controller = MakeController("QA", AuthConstants.DevBypassScheme);

        var result = controller.GetUser();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("QA", Prop(ok.Value!, "name"));
        Assert.Equal("QA", Prop(ok.Value!, "initials"));
    }

    [Fact]
    public void Initials_SingleWord_Short()
    {
        var controller = MakeController("QA", AuthConstants.DevBypassScheme);

        var result = controller.GetUser();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("QA", Prop(ok.Value!, "initials"));
    }

    [Fact]
    public void Initials_SingleWord_Long()
    {
        var controller = MakeController("John", AuthConstants.DevBypassScheme);

        var result = controller.GetUser();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("JO", Prop(ok.Value!, "initials"));
    }

    [Fact]
    public void Initials_TwoWords()
    {
        var controller = MakeController("Ada Lovelace", AuthConstants.DevBypassScheme);

        var result = controller.GetUser();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("AL", Prop(ok.Value!, "initials"));
    }

    [Fact]
    public void Initials_ThreeWords()
    {
        var controller = MakeController("Mary Ann Evans", AuthConstants.DevBypassScheme);

        var result = controller.GetUser();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("ME", Prop(ok.Value!, "initials"));
    }

    [Fact]
    public void Logout_DevBypass_Redirects_To_Root()
    {
        var controller = MakeController("QA", AuthConstants.DevBypassScheme);

        var result = controller.Logout();

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/", redirect.Url);
    }
}
