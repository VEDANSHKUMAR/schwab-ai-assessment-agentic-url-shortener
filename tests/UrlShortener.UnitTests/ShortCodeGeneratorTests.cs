using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Services;

namespace UrlShortener.UnitTests;

public class ShortCodeGeneratorTests
{
    private readonly ShortCodeGenerator _sut = new();

    [Fact]
    public void Generate_ReturnsExpectedLength()
    {
        var code = _sut.Generate(8);
        Assert.Equal(8, code.Length);
        Assert.True(ShortCodeGenerator.IsValidFormat(code));
    }

    [Fact]
    public void Generate_ProducesUniqueValues()
    {
        var set = Enumerable.Range(0, 200).Select(_ => _sut.Generate(8)).ToHashSet();
        Assert.True(set.Count > 190);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("docs-dotnet")]
    [InlineData("A1_b2")]
    public void IsValidFormat_AcceptsGoodAliases(string code) =>
        Assert.True(ShortCodeGenerator.IsValidFormat(code));

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("bad alias")]
    [InlineData("has!")]
    public void IsValidFormat_RejectsBadAliases(string code) =>
        Assert.False(ShortCodeGenerator.IsValidFormat(code));
}

public class ShortUrlTests
{
    [Fact]
    public void CanRedirect_False_WhenDisabledOrExpired()
    {
        var now = DateTimeOffset.Parse("2026-10-07T12:00:00Z");
        var url = ShortUrl.Create("abc12345", "https://example.com", now, now.AddHours(1));
        Assert.True(url.CanRedirect(now));

        url.Disable(now);
        Assert.False(url.CanRedirect(now));
    }

    [Fact]
    public void IsExpired_RespectsExpiresAt()
    {
        var created = DateTimeOffset.Parse("2026-10-07T12:00:00Z");
        var url = ShortUrl.Create("abc12345", "https://example.com", created, created.AddMinutes(5));
        Assert.False(url.IsExpired(created.AddMinutes(4)));
        Assert.True(url.IsExpired(created.AddMinutes(6)));
    }
}
