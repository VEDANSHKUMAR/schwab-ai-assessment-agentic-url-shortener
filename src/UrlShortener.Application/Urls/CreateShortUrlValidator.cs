using System.Net;
using FluentValidation;
using Microsoft.Extensions.Options;
using UrlShortener.Application.Options;
using UrlShortener.Domain.Services;

namespace UrlShortener.Application.Urls;

public sealed class CreateShortUrlValidator : AbstractValidator<CreateShortUrlRequest>
{
    public CreateShortUrlValidator(IOptions<UrlShortenerOptions> options)
    {
        var blocked = options.Value.BlockedDomains
            .Select(d => d.Trim().ToLowerInvariant())
            .Where(d => d.Length > 0)
            .ToHashSet();

        RuleFor(x => x.Url)
            .NotEmpty()
            .MaximumLength(2048)
            .Must(BeAbsoluteHttpUrl).WithMessage("URL must be an absolute http or https URL.")
            .Must(u => !LooksLikePrivateTarget(u)).WithMessage("URLs targeting private or loopback hosts are not allowed.")
            .Must(u => !IsBlockedDomain(u, blocked)).WithMessage("This destination domain is blocked.");

        RuleFor(x => x.CustomAlias)
            .MaximumLength(64)
            .Must(a => a is null || ShortCodeGenerator.IsValidFormat(a))
            .WithMessage("Custom alias must be 3-64 chars of letters, digits, '-' or '_'.");

        RuleFor(x => x.ExpiresAt)
            .Must(e => e is null || e > DateTimeOffset.UtcNow)
            .WithMessage("expiresAt must be in the future.");
    }

    private static bool BeAbsoluteHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && string.IsNullOrEmpty(uri.UserInfo);

    private static bool IsBlockedDomain(string url, HashSet<string> blocked)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return true;
        }

        var host = uri.Host.ToLowerInvariant();
        return blocked.Contains(host) || blocked.Any(b => host.EndsWith('.' + b, StringComparison.Ordinal));
    }

    /// <summary>
    /// Defense-in-depth for destination URLs. Note: browser redirect SSRF risk differs from
    /// server-side fetch; we still reject obvious private targets to reduce abuse.
    /// </summary>
    private static bool LooksLikePrivateTarget(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return true;
        }

        var host = uri.Host;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("metadata.google.internal", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.Equals("169.254.169.254", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IPAddress.TryParse(host, out var ip))
        {
            return IsPrivate(ip);
        }

        return false;
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && bytes.Length == 4)
        {
            return bytes[0] == 10
                   || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                   || (bytes[0] == 192 && bytes[1] == 168)
                   || (bytes[0] == 169 && bytes[1] == 254);
        }

        return false;
    }
}
