using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;
using UrlShortener.Application.Options;
using UrlShortener.Application.Urls;

namespace UrlShortener.UnitTests;

public class CreateShortUrlValidatorTests
{
    private readonly CreateShortUrlValidator _sut = new(
        Options.Create(new UrlShortenerOptions
        {
            BlockedDomains = ["malware.example"]
        }));

    [Fact]
    public void Accepts_HttpsUrl()
    {
        var result = _sut.TestValidate(new CreateShortUrlRequest("https://example.com/a", null, null));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Rejects_Ftp()
    {
        var result = _sut.TestValidate(new CreateShortUrlRequest("ftp://example.com/a", null, null));
        result.ShouldHaveValidationErrorFor(x => x.Url);
    }

    [Fact]
    public void Rejects_Localhost()
    {
        var result = _sut.TestValidate(new CreateShortUrlRequest("http://localhost/admin", null, null));
        result.ShouldHaveValidationErrorFor(x => x.Url);
    }

    [Fact]
    public void Rejects_BlockedDomain()
    {
        var result = _sut.TestValidate(new CreateShortUrlRequest("https://malware.example/x", null, null));
        result.ShouldHaveValidationErrorFor(x => x.Url);
    }

    [Fact]
    public void Rejects_PastExpiration()
    {
        var result = _sut.TestValidate(new CreateShortUrlRequest(
            "https://example.com",
            null,
            DateTimeOffset.UtcNow.AddMinutes(-1)));
        result.ShouldHaveValidationErrorFor(x => x.ExpiresAt);
    }
}
