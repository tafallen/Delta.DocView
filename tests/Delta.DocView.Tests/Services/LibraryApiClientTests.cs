using System.Net;
using System.Text;
using System.Text.Json;
using Delta.DocView.Client.Services;
using Delta.DocView.Shared;
using Delta.DocView.Shared.Models;

namespace Delta.DocView.Tests.Services;

public class LibraryApiClientTests
{
    private static readonly StepLibrary SampleLibrary = new()
    {
        Version = "1.0.0",
        GeneratedAt = "2026-01-01T00:00:00Z",
        GeneratorVersion = "1.0.0",
        Domains = [new StepDomain { Id = "Auth", Label = "Auth & Identity" }],
        Steps = [new Step
        {
            Id = "auth-001a2b3c", Type = "Given",
            Pattern = "I am logged in as {string}",
            Params = [new StepParam { Name = "username", Type = "string", Example = "\"admin@delta.io\"" }],
            File = "Auth/AuthSteps.cs", Line = 10, Domain = "Auth",
            Tags = ["login"], Used = 100,
            Description = "Logs in.", Source = "public void Login() {}",
            SuggestsNext = []
        }],
        Signature = new StepSignature { Algorithm = "SHA-256", Digest = new string('0', 64) }
    };

    [Fact]
    public async Task LoadAsync_SuccessResponse_StateBecomesLoaded()
    {
        var response = new LibraryResponse(SampleLibrary, null);
        var http = CreateMockHttpClient(HttpStatusCode.OK, response);
        var store = new ClientStepLibraryStore();
        var client = new LibraryApiClient(http, store);

        await client.LoadAsync();

        Assert.Equal(LoadingState.Loaded, client.State);
        Assert.Null(client.ErrorMessage);
        Assert.Single(store.Steps);
    }

    [Fact]
    public async Task LoadAsync_SuccessResponseWithWarning_StoresWarning()
    {
        var response = new LibraryResponse(SampleLibrary, "Signature mismatch.");
        var http = CreateMockHttpClient(HttpStatusCode.OK, response);
        var store = new ClientStepLibraryStore();
        var client = new LibraryApiClient(http, store);

        await client.LoadAsync();

        Assert.Equal(LoadingState.Loaded, client.State);
        Assert.Equal("Signature mismatch.", client.WarningMessage);
    }

    [Fact]
    public async Task LoadAsync_503Response_StateBecomesError()
    {
        var body = new { error = "Library file not found." };
        var http = CreateMockHttpClient(HttpStatusCode.ServiceUnavailable, body);
        var store = new ClientStepLibraryStore();
        var client = new LibraryApiClient(http, store);

        await client.LoadAsync();

        Assert.Equal(LoadingState.Error, client.State);
        Assert.Contains("Library file not found.", client.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_IsIdempotent()
    {
        var response = new LibraryResponse(SampleLibrary, null);
        var http = CreateMockHttpClient(HttpStatusCode.OK, response);
        var store = new ClientStepLibraryStore();
        var client = new LibraryApiClient(http, store);

        await client.LoadAsync();
        await client.LoadAsync(); // second call should be a no-op

        Assert.Equal(LoadingState.Loaded, client.State);
    }

    [Fact]
    public async Task LoadAsync_NetworkException_StateBecomesError()
    {
        var http = new HttpClient(new ThrowingHandler(new HttpRequestException("connection refused")))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        var store = new ClientStepLibraryStore();
        var client = new LibraryApiClient(http, store);

        await client.LoadAsync();

        Assert.Equal(LoadingState.Error, client.State);
        Assert.Contains("connection refused", client.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_EmptyBody200_StateBecomesError()
    {
        var http = new HttpClient(new RawHandler(HttpStatusCode.OK, "null"))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        var store = new ClientStepLibraryStore();
        var client = new LibraryApiClient(http, store);

        await client.LoadAsync();

        Assert.Equal(LoadingState.Error, client.State);
        Assert.Equal("Server returned an empty response.", client.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_ErrorBodyWithoutErrorProperty_FallsBackToStatusCodeMessage()
    {
        var body = new { detail = "some other shape" };
        var http = CreateMockHttpClient(HttpStatusCode.InternalServerError, body);
        var store = new ClientStepLibraryStore();
        var client = new LibraryApiClient(http, store);

        await client.LoadAsync();

        Assert.Equal(LoadingState.Error, client.State);
        Assert.Contains("500", client.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_MalformedErrorBodyJson_StillTransitionsToError()
    {
        var http = new HttpClient(new RawHandler(HttpStatusCode.BadGateway, "not json"))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        var store = new ClientStepLibraryStore();
        var client = new LibraryApiClient(http, store);

        await client.LoadAsync();

        Assert.Equal(LoadingState.Error, client.State);
        Assert.False(string.IsNullOrEmpty(client.ErrorMessage));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static HttpClient CreateMockHttpClient(HttpStatusCode status, object body)
    {
        var json = JsonSerializer.Serialize(body);
        var handler = new StubHttpMessageHandler(status, json);
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHttpMessageHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        private readonly Exception _ex;
        public ThrowingHandler(Exception ex) => _ex = ex;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(_ex);
    }

    private sealed class RawHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public RawHandler(HttpStatusCode status, string body) { _status = status; _body = body; }
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
    }
}
