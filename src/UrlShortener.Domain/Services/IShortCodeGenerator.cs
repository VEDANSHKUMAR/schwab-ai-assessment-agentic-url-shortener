namespace UrlShortener.Domain.Services;

public interface IShortCodeGenerator
{
    string Generate(int length = 8);
}
