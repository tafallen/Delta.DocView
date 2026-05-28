using Delta.DocView.Client.Services;

namespace Delta.DocView.Tests.Services;

public class InMemoryFavouritesStoreTests
{
    [Fact]
    public void Toggle_AddsThenRemoves()
    {
        var store = new InMemoryFavouritesStore();
        store.Toggle("s1");
        Assert.True(store.Has("s1"));
        store.Toggle("s1");
        Assert.False(store.Has("s1"));
    }

    [Fact]
    public void Count_ReflectsCurrentSize()
    {
        var store = new InMemoryFavouritesStore();
        Assert.Equal(0, store.Count);
        store.Toggle("a");
        store.Toggle("b");
        Assert.Equal(2, store.Count);
        store.Toggle("a");
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Has_TrueAfterAdd_FalseAfterRemove()
    {
        var store = new InMemoryFavouritesStore();
        store.Toggle("x");
        Assert.True(store.Has("x"));
        store.Toggle("x");
        Assert.False(store.Has("x"));
    }

    [Fact]
    public void Changed_RaisedOnEveryToggle()
    {
        var store = new InMemoryFavouritesStore();
        var raised = 0;
        store.Changed += () => raised++;

        store.Toggle("a");
        store.Toggle("a");
        store.Toggle("b");

        Assert.Equal(3, raised);
    }
}
