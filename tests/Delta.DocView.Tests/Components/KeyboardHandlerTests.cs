using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Delta.DocView.Tests.Components;

public class KeyboardHandlerTests : TestContext
{
    private (IRenderedComponent<KeyboardHandler> cut, IKeyboardActions actions) Render()
    {
        var actions = Substitute.For<IKeyboardActions>();
        Services.AddScoped(_ => actions);
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderComponent<KeyboardHandler>();
        return (cut, actions);
    }

    [Fact]
    public void OnKey_SelectNext_Calls_SelectNext()
    {
        var (cut, actions) = Render();
        cut.Instance.OnKey(KeyboardActionNames.SelectNext);
        actions.Received().SelectNext();
    }

    [Fact]
    public void OnKey_SelectPrev_Calls_SelectPrev()
    {
        var (cut, actions) = Render();
        cut.Instance.OnKey(KeyboardActionNames.SelectPrev);
        actions.Received().SelectPrev();
    }

    [Fact]
    public void OnKey_ToggleFav_Calls_ToggleSelectedFavourite()
    {
        var (cut, actions) = Render();
        cut.Instance.OnKey(KeyboardActionNames.ToggleFav);
        actions.Received().ToggleSelectedFavourite();
    }

    [Fact]
    public void OnKey_OpenPalette_Calls_OpenPalette()
    {
        var (cut, actions) = Render();
        cut.Instance.OnKey(KeyboardActionNames.OpenPalette);
        actions.Received().OpenPalette();
    }

    [Fact]
    public void OnKey_OpenShortcuts_Calls_OpenShortcuts()
    {
        var (cut, actions) = Render();
        cut.Instance.OnKey(KeyboardActionNames.OpenShortcuts);
        actions.Received().OpenShortcuts();
    }

    [Fact]
    public void OnKey_ToggleComposer_Calls_ToggleComposer()
    {
        var (cut, actions) = Render();
        cut.Instance.OnKey(KeyboardActionNames.ToggleComposer);
        actions.Received().ToggleComposer();
    }

    [Fact]
    public void OnKey_CloseOverlay_Calls_CloseOverlay()
    {
        var (cut, actions) = Render();
        cut.Instance.OnKey(KeyboardActionNames.CloseOverlay);
        actions.Received().CloseOverlay();
    }

    [Fact]
    public void OnKey_UnknownAction_NoOp()
    {
        var (cut, actions) = Render();
        cut.Instance.OnKey("bogus");
        actions.DidNotReceiveWithAnyArgs().SelectNext();
        actions.DidNotReceiveWithAnyArgs().OpenPalette();
    }
}
