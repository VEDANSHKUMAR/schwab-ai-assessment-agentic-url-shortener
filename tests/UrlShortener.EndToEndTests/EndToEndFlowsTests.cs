using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using UrlShortener.EndToEndTests.Infrastructure;

namespace UrlShortener.EndToEndTests;

public sealed class EndToEndFlowsTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public EndToEndFlowsTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
    }

    private HttpClient CreateAnonymousClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Product_Flow_Create_Persist_Redirect_Analytics()
    {
        var alias = "e2e-" + Guid.NewGuid().ToString("N")[..8];

        var created = await _client.PostAsJsonAsync("/api/v1/urls", new
        {
            url = "https://learn.microsoft.com/aspnet/core",
            customAlias = alias,
            expiresAt = DateTimeOffset.UtcNow.AddDays(30)
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var details = await _client.GetAsync($"/api/v1/urls/{alias}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var detailBody = await details.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.True(detailBody.GetProperty("isActive").GetBoolean());

        using var anon = CreateAnonymousClient();
        var redirect = await anon.GetAsync("/" + alias);
        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);

        await Task.Delay(1000);
        var analytics = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/urls/{alias}/analytics");
        Assert.True(analytics.GetProperty("totalClicks").GetInt64() >= 1);

        var disable = await _client.DeleteAsync($"/api/v1/urls/{alias}");
        Assert.Equal(HttpStatusCode.NoContent, disable.StatusCode);
        var afterDisable = await anon.GetAsync("/" + alias);
        Assert.Equal(HttpStatusCode.Gone, afterDisable.StatusCode);
    }

    [Theory]
    [InlineData("Greenfield")]
    [InlineData("Brownfield")]
    [InlineData("Ambiguous")]
    public async Task Orchestration_Scenario_Completes_With_Approvals(string scenario)
    {
        var start = await _client.PostAsJsonAsync("/api/v1/orchestration/runs", new
        {
            scenario,
            brief = $"E2E validation for {scenario}"
        });
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);
        var run = await start.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var id = run.GetProperty("id").GetGuid();
        var status = run.GetProperty("status").GetString();

        var guard = 0;
        while (status is "WaitingApproval" or "Running" or "Pending" && guard++ < 20)
        {
            if (status == "WaitingApproval")
            {
                var approve = await _client.PostAsJsonAsync($"/api/v1/orchestration/runs/{id}/approve", new
                {
                    approve = true,
                    comment = "E2E grader approval"
                });
                Assert.True(approve.IsSuccessStatusCode);
                run = await approve.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
            }
            else
            {
                var tick = await _client.PostAsync($"/api/v1/orchestration/runs/{id}/tick", null);
                Assert.True(tick.IsSuccessStatusCode);
                run = await tick.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
            }

            status = run!.GetProperty("status").GetString();
        }

        Assert.Equal("Succeeded", status);
        Assert.True(run.GetProperty("context").EnumerateObject().Any());
        Assert.Contains(run.GetProperty("events").EnumerateArray(), e =>
            e.GetProperty("kind").GetString() == "ApprovalGranted");
    }

    [Fact]
    public async Task Orchestration_Reject_Fails_Run_Safely()
    {
        var start = await _client.PostAsJsonAsync("/api/v1/orchestration/runs", new
        {
            scenario = "Greenfield"
        });
        var run = await start.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var id = run.GetProperty("id").GetGuid();

        var reject = await _client.PostAsJsonAsync($"/api/v1/orchestration/runs/{id}/approve", new
        {
            approve = false,
            comment = "Rejected in e2e"
        });
        var after = await reject.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal("Failed", after.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Orchestration_SafeStop_Works()
    {
        var start = await _client.PostAsJsonAsync("/api/v1/orchestration/runs", new
        {
            scenario = "Brownfield"
        });
        var run = await start.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var id = run.GetProperty("id").GetGuid();

        var stop = await _client.PostAsync($"/api/v1/orchestration/runs/{id}/stop", null);
        var after = await stop.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal("Stopped", after.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Orchestration_Replan_Invalidates_Downstream_And_Updates_Metrics()
    {
        var start = await _client.PostAsJsonAsync("/api/v1/orchestration/runs", new
        {
            scenario = "Greenfield"
        });
        var run = await start.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var id = run.GetProperty("id").GetGuid();

        // Approve architecture gate once so design stages exist
        if (run.GetProperty("status").GetString() == "WaitingApproval")
        {
            var approved = await _client.PostAsJsonAsync($"/api/v1/orchestration/runs/{id}/approve", new
            {
                approve = true,
                comment = "pre-replan"
            });
            run = await approved.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        }

        var replan = await _client.PostAsJsonAsync($"/api/v1/orchestration/runs/{id}/replan", new
        {
            brief = "Replanned brief after ambiguity clarification",
            changedArtifactKey = "requirements.json",
            changedArtifactValue = """{"replanned":true}""",
            reason = "upstream requirements changed"
        });
        Assert.True(replan.IsSuccessStatusCode);
        var after = await replan.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.True(after.GetProperty("replanCount").GetInt32() >= 1);
        Assert.True(after.GetProperty("contextVersion").GetInt32() >= 2);
        Assert.Contains(after.GetProperty("events").EnumerateArray(), e =>
            e.GetProperty("kind").GetString() == "Replan");

        var metrics = await _client.GetFromJsonAsync<JsonElement>("/api/v1/orchestration/metrics");
        Assert.True(metrics.GetProperty("replanCount").GetInt64() >= 1);
        Assert.True(metrics.TryGetProperty("mttrMs", out _));
        Assert.True(metrics.TryGetProperty("averageEndToEndLatencyMs", out _));
    }
}
