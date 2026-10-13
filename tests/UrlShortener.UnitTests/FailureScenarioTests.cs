using FluentValidation.TestHelper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UrlShortener.Application.Options;
using UrlShortener.Application.Urls;
using UrlShortener.Domain.Entities;
using UrlShortener.Orchestration.Agents;
using UrlShortener.Orchestration.Models;
using UrlShortener.Orchestration.Services;

namespace UrlShortener.UnitTests;

public class FailureScenarioTests
{
    [Fact]
    public void ShortUrl_Rejects_Expiration_In_Past_Relative_To_Create()
    {
        var now = DateTimeOffset.Parse("2026-10-07T12:00:00Z");
        Assert.Throws<ArgumentException>(() =>
            ShortUrl.Create("code1234", "https://example.com", now, now.AddMinutes(-1)));
    }

    [Fact]
    public void Validator_Rejects_Malformed_Alias()
    {
        var sut = new CreateShortUrlValidator(Options.Create(new UrlShortenerOptions()));
        var result = sut.TestValidate(new CreateShortUrlRequest("https://example.com", "bad alias!", null));
        result.ShouldHaveValidationErrorFor(x => x.CustomAlias);
    }

    [Fact]
    public async Task Orchestration_Reject_Increments_Failed_Metrics()
    {
        var sut = new OrchestrationService(new DeterministicStageAgent(), NullLogger<OrchestrationService>.Instance);
        var started = await sut.StartAsync(new StartRunRequest(ScenarioKind.Greenfield, null), CancellationToken.None);
        await sut.TickAsync(started.Id, CancellationToken.None);
        await sut.ApproveAsync(started.Id, new ApprovalRequest(false, "no"), CancellationToken.None);
        var metrics = sut.GetMetrics();
        Assert.True(metrics.TotalRuns >= 1);
        Assert.True(metrics.FailedRuns >= 1);
    }

    [Fact]
    public void GoneException_Is_DomainException()
    {
        var ex = new UrlShortener.Domain.Exceptions.GoneException("expired");
        Assert.IsAssignableFrom<UrlShortener.Domain.Exceptions.DomainException>(ex);
    }
}
