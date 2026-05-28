using Bunit;
using Delta.DocView.Client.Components;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Delta.DocView.Tests.Components;

public class LeftRailTests : TestContext
{
    private void RegisterServices()
    {
        var store = new ClientStepLibraryStore();
        store.Populate(new StepLibrary
        {
            Version = "1.0.0",
            GeneratedAt = "2026-01-01T00:00:00Z",
            Domains = [new StepDomain { Id = "Auth", Label = "Auth & Identity" }],
            Steps =
            [
                new Step
                {
                    Id = "s1",
                    Type = "Given",
                    Pattern = "I have {int} items",
                    Domain = "Auth",
                    Params = [new StepParam { Name = "count", Type = "int" }]
                }
            ],
            Signature = new StepSignature { Algorithm = "SHA-256", Digest = new string('0', 64) }
        });

        Services.AddScoped(_ => store);
        Services.AddScoped<FilterState>();
        Services.AddScoped<IFavouritesStore, InMemoryFavouritesStore>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void LeftRail_RendersAllFourFilters_InOrder()
    {
        RegisterServices();

        var cut = RenderComponent<LeftRail>();

        var shell = cut.Find(".left-rail-shell");
        var markup = shell.InnerHtml;

        var stepTypeIdx = markup.IndexOf("step-type-filter", StringComparison.Ordinal);
        var domainIdx = markup.IndexOf("domain-filter", StringComparison.Ordinal);
        var paramTypeIdx = markup.IndexOf("param-type-filter", StringComparison.Ordinal);
        var favIdx = markup.IndexOf("data-testid=\"favourites-toggle\"", StringComparison.Ordinal);

        Assert.True(stepTypeIdx >= 0, "step-type-filter not found");
        Assert.True(domainIdx > stepTypeIdx, "domain-filter not after step-type-filter");
        Assert.True(paramTypeIdx > domainIdx, "param-type-filter not after domain-filter");
        Assert.True(favIdx > paramTypeIdx, "favourites-toggle not after param-type-filter");
    }
}
