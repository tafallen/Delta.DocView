using Delta.DocView.Client.Services;
using Microsoft.JSInterop;
using NSubstitute;

namespace Delta.DocView.Tests.Services;

public class LocalStorageFavouritesStoreTests
{
    [Fact]
    public async Task InitializeAsync_NoStoredValue_LeavesStoreEmpty()
    {
        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<string>("docview.favourites.read", Arg.Any<object?[]?>())
            .Returns(ValueTask.FromResult("[]"));

        var store = new LocalStorageFavouritesStore(js);
        await store.InitializeAsync();

        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task InitializeAsync_RestoresStoredIds()
    {
        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<string>("docview.favourites.read", Arg.Any<object?[]?>())
            .Returns(ValueTask.FromResult("[\"a\",\"b\"]"));

        var store = new LocalStorageFavouritesStore(js);
        await store.InitializeAsync();

        Assert.True(store.Has("a"));
        Assert.True(store.Has("b"));
        Assert.Equal(2, store.Count);
    }

    [Fact]
    public async Task InitializeAsync_MalformedJson_LeavesStoreEmpty_NoThrow()
    {
        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<string>("docview.favourites.read", Arg.Any<object?[]?>())
            .Returns(ValueTask.FromResult("{not valid json}"));

        var store = new LocalStorageFavouritesStore(js);
        await store.InitializeAsync();

        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task InitializeAsync_NullJsonValue_LeavesStoreEmpty()
    {
        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<string>("docview.favourites.read", Arg.Any<object?[]?>())
            .Returns(ValueTask.FromResult("null"));

        var store = new LocalStorageFavouritesStore(js);
        await store.InitializeAsync();

        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_Throws()
    {
        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<string>("docview.favourites.read", Arg.Any<object?[]?>())
            .Returns(ValueTask.FromResult("[]"));

        var store = new LocalStorageFavouritesStore(js);
        await store.InitializeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.InitializeAsync());
    }

    [Fact]
    public async Task Toggle_AddsThenRemoves_RaisesChangedEachTime()
    {
        var js = Substitute.For<IJSRuntime>();
        var store = new LocalStorageFavouritesStore(js);

        var changedCount = 0;
        store.Changed += () => changedCount++;

        store.Toggle("x");
        store.Toggle("x");

        // Let fire-and-forget writes settle.
        await Task.Yield();
        await Task.Yield();

        Assert.Equal(2, changedCount);
        Assert.False(store.Has("x"));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task Toggle_WritesOrdinalSortedJsonViaJs()
    {
        var js = new RecordingJsRuntime();
        var store = new LocalStorageFavouritesStore(js);

        store.Toggle("b");
        store.Toggle("a");

        // Let fire-and-forget writes settle.
        await Task.Yield();
        await Task.Yield();

        Assert.NotEmpty(js.WriteCalls);
        var lastJson = js.WriteCalls[^1];
        Assert.Equal("[\"a\",\"b\"]", lastJson);
    }

    /// <summary>
    /// Hand-rolled <see cref="IJSRuntime"/> stub for the write-assertion test.
    /// NSubstitute's <c>params object?[]?</c> capture for <c>InvokeVoidAsync</c>
    /// is awkward; recording the JSON argument directly is cleaner.
    /// </summary>
    private sealed class RecordingJsRuntime : IJSRuntime
    {
        public List<string> WriteCalls { get; } = new();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "docview.favourites.write" && args is { Length: > 0 } && args[0] is string s)
            {
                WriteCalls.Add(s);
            }
            return new ValueTask<TValue>(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }
}
