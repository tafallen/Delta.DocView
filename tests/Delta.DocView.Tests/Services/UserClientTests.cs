using System.Net;
using System.Text;
using Delta.DocView.Client.Services;

namespace Delta.DocView.Tests.Services;

public class UserClientTests
{
    [Fact]
    public async Task LoadAsync_Success_PopulatesCurrent()
    {
        var http = MakeHttpClient(HttpStatusCode.OK,
            """{"name":"Ada Lovelace","initials":"AL","authenticated":true}""");
        var client = new UserClient(http);

        await client.LoadAsync();

        Assert.Equal("Ada Lovelace", client.Current.Name);
        Assert.Equal("AL", client.Current.Initials);
        Assert.True(client.Current.Authenticated);
    }

    [Fact]
    public async Task LoadAsync_NetworkFailure_KeepsFallback()
    {
        var http = new HttpClient(new ThrowingHandler(new HttpRequestException("connection refused")))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        var client = new UserClient(http);

        await client.LoadAsync(); // must not throw

        Assert.Equal(UserInfo.Fallback, client.Current);
    }

    [Fact]
    public async Task LoadAsync_NullBody_KeepsFallback()
    {
        var http = MakeHttpClient(HttpStatusCode.OK, "null");
        var client = new UserClient(http);

        await client.LoadAsync();

        Assert.Equal(UserInfo.Fallback, client.Current);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static HttpClient MakeHttpClient(HttpStatusCode status, string body) =>
        new HttpClient(new StubHandler(status, body)) { BaseAddress = new Uri("http://localhost/") };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body) { _status = status; _body = body; }

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
}
