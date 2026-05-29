using Delta.DocView.Client.Services;
using Microsoft.JSInterop;
using NSubstitute;

namespace Delta.DocView.Tests.Services;

public class TweaksStoreTests
{
    [Fact]
    public void Defaults_Before_Init()
    {
        var store = new TweaksStore(Substitute.For<IJSRuntime>());

        Assert.False(store.Dark);
        Assert.Equal(AccentOption.Orange, store.Accent);
        Assert.Equal(DensityOption.Comfortable, store.Density);
        Assert.Equal(RowEmphasisOption.Pattern, store.RowEmphasis);
        Assert.Equal(SourceDefaultOption.Collapsed, store.SourceDefault);
    }

    [Fact]
    public async Task InitializeAsync_NoStored_FallsBackToOsDark()
    {
        var js = new RecordingJsRuntime { ReadResponse = "", PrefersDarkResponse = true };
        var store = new TweaksStore(js);

        await store.InitializeAsync();

        Assert.True(store.Dark);
    }

    [Fact]
    public async Task InitializeAsync_RestoresStoredValues()
    {
        var js = new RecordingJsRuntime
        {
            ReadResponse = "{\"dark\":false,\"accent\":\"blue\",\"density\":\"compact\",\"rowEmphasis\":\"meta\",\"source\":\"expanded\"}",
            PrefersDarkResponse = true // should NOT be consulted
        };
        var store = new TweaksStore(js);

        await store.InitializeAsync();

        Assert.False(store.Dark);
        Assert.Equal(AccentOption.Blue, store.Accent);
        Assert.Equal(DensityOption.Compact, store.Density);
        Assert.Equal(RowEmphasisOption.Meta, store.RowEmphasis);
        Assert.Equal(SourceDefaultOption.Expanded, store.SourceDefault);
        Assert.Equal(0, js.PrefersDarkCalls);
    }

    [Fact]
    public async Task InitializeAsync_MalformedJson_UsesDefaults_NoThrow()
    {
        var js = new RecordingJsRuntime { ReadResponse = "{not json" };
        var store = new TweaksStore(js);

        await store.InitializeAsync();

        Assert.False(store.Dark);
        Assert.Equal(AccentOption.Orange, store.Accent);
        Assert.Equal(DensityOption.Comfortable, store.Density);
        Assert.Equal(RowEmphasisOption.Pattern, store.RowEmphasis);
        Assert.Equal(SourceDefaultOption.Collapsed, store.SourceDefault);
    }

    [Fact]
    public async Task SetAccent_PersistsAndAppliesRoot_AndRaisesChanged()
    {
        var js = new RecordingJsRuntime { ReadResponse = "" };
        var store = new TweaksStore(js);
        await store.InitializeAsync();

        var changed = 0;
        store.Changed += () => changed++;

        store.SetAccent(AccentOption.Blue);
        await Task.Yield();
        await Task.Yield();

        Assert.Equal(1, changed);
        Assert.NotEmpty(js.WriteCalls);
        Assert.NotEmpty(js.ApplyRootCalls);
        Assert.Equal("blue", js.ApplyRootCalls[^1].accent);
    }

    [Fact]
    public async Task SetDark_RoundTrips_InWrittenJson()
    {
        var js = new RecordingJsRuntime { ReadResponse = "" };
        var store = new TweaksStore(js);
        await store.InitializeAsync();

        store.SetDark(true);
        await Task.Yield();
        await Task.Yield();

        Assert.NotEmpty(js.WriteCalls);
        Assert.Contains("\"dark\":true", js.WriteCalls[^1]);
    }

    [Fact]
    public async Task SetSourceDefault_Persists_ButDoesNotApplyRoot()
    {
        var js = new RecordingJsRuntime { ReadResponse = "" };
        var store = new TweaksStore(js);
        await store.InitializeAsync();

        var applyBefore = js.ApplyRootCalls.Count;

        store.SetSourceDefault(SourceDefaultOption.Expanded);
        await Task.Yield();
        await Task.Yield();

        Assert.NotEmpty(js.WriteCalls);
        Assert.Equal(applyBefore, js.ApplyRootCalls.Count);
        Assert.Contains("\"source\":\"expanded\"", js.WriteCalls[^1]);
    }

    [Theory]
    [InlineData(AccentOption.Orange, "orange")]
    [InlineData(AccentOption.Blue, "blue")]
    [InlineData(AccentOption.Violet, "violet")]
    [InlineData(DensityOption.Comfortable, "comfortable")]
    [InlineData(DensityOption.Compact, "compact")]
    [InlineData(RowEmphasisOption.Pattern, "pattern")]
    [InlineData(RowEmphasisOption.Meta, "meta")]
    public void Enum_ToString_Lowercase_Matches_Css_Token(Enum value, string expectedToken)
        => Assert.Equal(expectedToken, value.ToString().ToLowerInvariant());

    // RootAttributes() pins the C# side of the user-visible DOM contract: the exact
    // (dark, accent, density, rowEmphasis) token tuple fed to docview.tweaks.applyRoot.
    // bUnit has no JS engine, so it cannot execute the real applyRoot setAttribute calls
    // nor the index.html pre-paint script — those remain browser-only and are verified
    // manually until an integration harness lands. These tests pin everything up to the
    // call boundary; the JS setAttribute side is the only genuinely-untested seam.

    [Fact]
    public void RootAttributes_Defaults()
    {
        var store = new TweaksStore(Substitute.For<IJSRuntime>());

        Assert.Equal((false, "orange", "comfortable", "pattern"), store.RootAttributes());
    }

    [Fact]
    public async Task RootAttributes_After_SetAccent_Blue()
    {
        var js = new RecordingJsRuntime { ReadResponse = "" };
        var store = new TweaksStore(js);
        await store.InitializeAsync();

        store.SetAccent(AccentOption.Blue);

        Assert.Equal((false, "blue", "comfortable", "pattern"), store.RootAttributes());
    }

    [Fact]
    public async Task RootAttributes_After_Multiple_Sets()
    {
        var js = new RecordingJsRuntime { ReadResponse = "" };
        var store = new TweaksStore(js);
        await store.InitializeAsync();

        store.SetDark(true);
        store.SetDensity(DensityOption.Compact);
        store.SetRowEmphasis(RowEmphasisOption.Meta);

        Assert.Equal((true, "orange", "compact", "meta"), store.RootAttributes());
    }

    [Fact]
    public async Task Setter_SameValue_NoChange_NoEvent()
    {
        var js = new RecordingJsRuntime { ReadResponse = "" };
        var store = new TweaksStore(js);
        await store.InitializeAsync();

        var changed = 0;
        store.Changed += () => changed++;

        store.SetAccent(store.Accent);
        await Task.Yield();
        await Task.Yield();

        Assert.Equal(0, changed);
    }

    [Fact]
    public async Task InitializeAsync_SecondCall_NoOps()
    {
        var js = new RecordingJsRuntime { ReadResponse = "" };
        var store = new TweaksStore(js);

        await store.InitializeAsync();
        await store.InitializeAsync();

        Assert.Equal(1, js.ReadCalls);
    }

    [Fact]
    public void Defaults_Before_Init_FollowOs_False()
    {
        var store = new TweaksStore(Substitute.For<IJSRuntime>());
        Assert.False(store.FollowOs);
    }

    [Fact]
    public async Task SetFollowOs_True_CallsWatchOs_AndRaisesChanged()
    {
        var js = new RecordingJsRuntime { ReadResponse = "" };
        var store = new TweaksStore(js);
        await store.InitializeAsync();
        var changed = 0;
        store.Changed += () => changed++;

        store.SetFollowOs(true);
        await Task.Yield();
        await Task.Yield();

        Assert.True(store.FollowOs);
        Assert.Equal(1, changed);
        Assert.Equal(1, js.WatchOsCalls);
    }

    [Fact]
    public async Task SetFollowOs_False_CallsUnwatchOs_AndRaisesChanged()
    {
        var js = new RecordingJsRuntime { ReadResponse = "" };
        var store = new TweaksStore(js);
        await store.InitializeAsync();
        store.SetFollowOs(true);
        await Task.Yield();
        await Task.Yield();
        var changed = 0;
        store.Changed += () => changed++;

        store.SetFollowOs(false);
        await Task.Yield();
        await Task.Yield();

        Assert.False(store.FollowOs);
        Assert.Equal(1, changed);
        Assert.Equal(1, js.UnwatchOsCalls);
    }

    [Fact]
    public async Task SetFollowOs_SameValue_NoChange_NoEvent()
    {
        var js = new RecordingJsRuntime { ReadResponse = "" };
        var store = new TweaksStore(js);
        await store.InitializeAsync();
        var changed = 0;
        store.Changed += () => changed++;

        store.SetFollowOs(false); // already false

        Assert.Equal(0, changed);
        Assert.Equal(0, js.WatchOsCalls);
    }

    [Fact]
    public void OnOsColorSchemeChanged_WhenFollowOs_SetsDark()
    {
        var js = new RecordingJsRuntime { ReadResponse = "" };
        var store = new TweaksStore(js);
        store.SetFollowOs(true);

        store.OnOsColorSchemeChanged(true);

        Assert.True(store.Dark);
    }

    [Fact]
    public void OnOsColorSchemeChanged_WhenNotFollowOs_IsNoOp()
    {
        var js = new RecordingJsRuntime { ReadResponse = "" };
        var store = new TweaksStore(js);
        // FollowOs defaults to false

        store.OnOsColorSchemeChanged(true);

        Assert.False(store.Dark); // unchanged
    }

    [Fact]
    public async Task Dispose_CallsUnwatchOs()
    {
        var js = new RecordingJsRuntime { ReadResponse = "" };
        var store = new TweaksStore(js);
        await store.InitializeAsync();
        store.SetFollowOs(true);
        await Task.Yield();
        await Task.Yield();

        store.Dispose();
        await Task.Yield();
        await Task.Yield();

        Assert.True(js.UnwatchOsCalls >= 1);
    }

    [Fact]
    public async Task InitializeAsync_WithFollowOs_RestoresListenerAndUsesOsDark()
    {
        var js = new RecordingJsRuntime
        {
            ReadResponse = "{\"dark\":false,\"followOs\":true,\"accent\":\"orange\",\"density\":\"comfortable\",\"rowEmphasis\":\"pattern\",\"source\":\"collapsed\"}",
            PrefersDarkResponse = true
        };
        var store = new TweaksStore(js);

        await store.InitializeAsync();
        await Task.Yield();
        await Task.Yield();

        Assert.True(store.FollowOs);
        Assert.True(store.Dark);   // from OS preference, not stored dark:false
        Assert.Equal(1, js.WatchOsCalls);
    }

    [Fact]
    public async Task SetFollowOs_Persists_FollowOs_InJson()
    {
        var js = new RecordingJsRuntime { ReadResponse = "" };
        var store = new TweaksStore(js);
        await store.InitializeAsync();

        store.SetFollowOs(true);
        await Task.Yield();
        await Task.Yield();

        Assert.NotEmpty(js.WriteCalls);
        Assert.Contains("\"followOs\":true", js.WriteCalls[^1]);
    }

    [Fact]
    public async Task SetFollowOs_True_ImmediatelySyncsOsDarkPreference()
    {
        var js = new RecordingJsRuntime { ReadResponse = "", PrefersDarkResponse = true };
        var store = new TweaksStore(js);
        await store.InitializeAsync(); // Dark=true from OS, but let's override
        store.SetDark(false);          // manually set to false
        await Task.Yield();
        await Task.Yield();

        store.SetFollowOs(true);
        await Task.Yield();
        await Task.Yield();

        Assert.True(store.Dark); // immediately reflects OS preference (true)
    }

    /// <summary>
    /// Hand-rolled <see cref="IJSRuntime"/> stub: returns a preset string for
    /// <c>docview.tweaks.read</c>, a bool for <c>docview.prefersDark</c>, and records
    /// <c>docview.tweaks.write</c> / <c>applyRoot</c> calls. Mirrors the stub in
    /// <c>LocalStorageFavouritesStoreTests</c>.
    /// </summary>
    private sealed class RecordingJsRuntime : IJSRuntime
    {
        public string? ReadResponse { get; set; }
        public bool PrefersDarkResponse { get; set; }

        public int ReadCalls { get; private set; }
        public int PrefersDarkCalls { get; private set; }
        public int WatchOsCalls { get; private set; }
        public int UnwatchOsCalls { get; private set; }
        public List<string> WriteCalls { get; } = new();
        public List<(bool dark, string accent, string density, string rowEmphasis)> ApplyRootCalls { get; } = new();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            switch (identifier)
            {
                case "docview.tweaks.read":
                    ReadCalls++;
                    if (ReadResponse is TValue tv) return new ValueTask<TValue>(tv);
                    break;
                case "docview.prefersDark":
                    PrefersDarkCalls++;
                    if (PrefersDarkResponse is TValue pv) return new ValueTask<TValue>(pv);
                    break;
                case "docview.tweaks.write":
                    if (args is { Length: > 0 } && args[0] is string s) WriteCalls.Add(s);
                    break;
                case "docview.tweaks.applyRoot":
                    if (args is { Length: >= 4 }
                        && args[0] is bool dark && args[1] is string accent
                        && args[2] is string density && args[3] is string rowEmphasis)
                    {
                        ApplyRootCalls.Add((dark, accent, density, rowEmphasis));
                    }
                    break;
                case "docview.tweaks.watchOs":
                    WatchOsCalls++;
                    break;
                case "docview.tweaks.unwatchOs":
                    UnwatchOsCalls++;
                    break;
            }
            return new ValueTask<TValue>(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }
}
