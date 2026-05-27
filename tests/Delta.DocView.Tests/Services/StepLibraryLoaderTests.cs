using Delta.DocView.Server.Services;

namespace Delta.DocView.Tests.Services;

public class StepLibraryLoaderTests
{
    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    [Fact]
    public void Load_ValidFile_ReturnsLibraryAndRawJson()
    {
        var path = Path.Combine(TestDataDir, "valid-library.json");
        var loader = new StepLibraryLoader();

        var (library, rawJson) = loader.Load(path);

        Assert.Equal("1.0.0", library.Version);
        Assert.Single(library.Steps);
        Assert.Equal("auth-001a2b3c", library.Steps[0].Id);
        Assert.NotEmpty(rawJson);
    }

    [Fact]
    public void Load_MissingFile_ThrowsFileNotFoundException()
    {
        var loader = new StepLibraryLoader();

        var ex = Assert.Throws<FileNotFoundException>(
            () => loader.Load("/nonexistent/path/library.json"));

        Assert.Contains("/nonexistent/path/library.json", ex.Message);
    }

    [Fact]
    public void Load_InvalidJson_ThrowsJsonException()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "this is not json {{{");
            var loader = new StepLibraryLoader();

            Assert.Throws<System.Text.Json.JsonException>(() => loader.Load(path));
        }
        finally { File.Delete(path); }
    }
}
