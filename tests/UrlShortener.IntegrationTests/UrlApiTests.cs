using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using UrlShortener.IntegrationTests.Infrastructure;

namespace UrlShortener.IntegrationTests;

public sealed class UrlApiTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public UrlApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        _client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
    }

    private HttpClient CreateAnonymousClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Health_And_Ready_Succeed()
    {
        var health = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        var ready = await _client.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Fact]
    public async Task Create_Redirect_And_Analytics_Flow()
    {
        var alias = "it-" + Guid.NewGuid().ToString("N")[..8];
        var create = await _client.PostAsJsonAsync("/api/v1/urls", new
        {
            url = "https://example.com/integration",
            customAlias = alias
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var body = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal(alias, body.GetProperty("shortCode").GetString());

        using var redirectClient = CreateAnonymousClient();
        var redirect = await redirectClient.GetAsync("/" + alias);
        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
        Assert.Equal("https://example.com/integration", redirect.Headers.Location?.ToString());

        var redirect2 = await redirectClient.GetAsync("/" + alias);
        Assert.Equal(HttpStatusCode.Redirect, redirect2.StatusCode);

        await Task.Delay(800);
        var analytics = await _client.GetAsync($"/api/v1/urls/{alias}/analytics");
        Assert.Equal(HttpStatusCode.OK, analytics.StatusCode);
        var analyticsBody = await analytics.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.True(analyticsBody.GetProperty("totalClicks").GetInt64() >= 1);
    }

    [Fact]
    public async Task Duplicate_Alias_Returns_Conflict()
    {
        var alias = "dup-" + Guid.NewGuid().ToString("N")[..8];
        var first = await _client.PostAsJsonAsync("/api/v1/urls", new
        {
            url = "https://example.com/a",
            customAlias = alias
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/api/v1/urls", new
        {
            url = "https://example.com/b",
            customAlias = alias
        });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Disable_Then_Redirect_Returns_Gone()
    {
        var alias = "dis-" + Guid.NewGuid().ToString("N")[..8];
        var create = await _client.PostAsJsonAsync("/api/v1/urls", new
        {
            url = "https://example.com/disabled",
            customAlias = alias
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var delete = await _client.DeleteAsync($"/api/v1/urls/{alias}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        using var redirectClient = CreateAnonymousClient();
        var redirect = await redirectClient.GetAsync("/" + alias);
        Assert.Equal(HttpStatusCode.Gone, redirect.StatusCode);
    }

    [Fact]
    public async Task Invalid_And_Private_Urls_Return_BadRequest()
    {
        var ftp = await _client.PostAsJsonAsync("/api/v1/urls", new { url = "ftp://example.com/x" });
        Assert.Equal(HttpStatusCode.BadRequest, ftp.StatusCode);

        var loopback = await _client.PostAsJsonAsync("/api/v1/urls", new { url = "http://127.0.0.1/secret" });
        Assert.Equal(HttpStatusCode.BadRequest, loopback.StatusCode);

        var blocked = await _client.PostAsJsonAsync("/api/v1/urls", new { url = "https://malware.example/x" });
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
    }

    [Fact]
    public async Task Missing_Api_Key_Returns_Unauthorized()
    {
        using var anon = CreateAnonymousClient();
        var res = await anon.PostAsJsonAsync("/api/v1/urls", new { url = "https://example.com" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Idempotency_Key_Returns_Same_Short_Code()
    {
        var key = "idem-" + Guid.NewGuid().ToString("N");
        using var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/urls")
        {
            Content = JsonContent.Create(new { url = "https://example.com/idem" })
        };
        req1.Headers.Add("Idempotency-Key", key);
        var r1 = await _client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        var b1 = await r1.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        using var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/urls")
        {
            Content = JsonContent.Create(new { url = "https://example.com/idem" })
        };
        req2.Headers.Add("Idempotency-Key", key);
        var r2 = await _client.SendAsync(req2);
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
        var b2 = await r2.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        Assert.Equal(b1.GetProperty("shortCode").GetString(), b2.GetProperty("shortCode").GetString());
    }

    [Fact]
    public async Task Expired_Url_Returns_Gone_On_Redirect()
    {
        var alias = "exp-" + Guid.NewGuid().ToString("N")[..8];
        var expires = DateTimeOffset.UtcNow.AddSeconds(2);
        var create = await _client.PostAsJsonAsync("/api/v1/urls", new
        {
            url = "https://example.com/expiring",
            customAlias = alias,
            expiresAt = expires
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        await Task.Delay(2500);
        using var redirectClient = CreateAnonymousClient();
        var redirect = await redirectClient.GetAsync("/" + alias);
        Assert.Equal(HttpStatusCode.Gone, redirect.StatusCode);
    }
}
