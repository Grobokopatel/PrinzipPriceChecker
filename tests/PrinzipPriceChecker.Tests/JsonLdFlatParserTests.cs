using NUnit.Framework;
using PrinzipPriceChecker.Api.Parsing;

namespace PrinzipPriceChecker.Tests;

[TestFixture]
public class JsonLdFlatParserTests
{
    private readonly JsonLdFlatParser _parser = new();

    [Test]
    public async Task Parse_RealPageSnapshot_ReturnsPriceFromJsonLd()
    {
        var html = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "TestData", "shartashpark-65040.html"),
            CancellationToken.None);

        var snapshot = _parser.Parse(html);

        Assert.That(snapshot.Price, Is.EqualTo(7_711_200));
        Assert.That(snapshot.Currency, Is.EqualTo("RUB"));
        Assert.That(snapshot.Name, Is.EqualTo("Квартира с кухней-гостиной и двумя комнатами"));
        Assert.That(snapshot.Description, Is.EqualTo("Шарташ Парк, 1 дом, 1 дом, кв. № 398"));
        Assert.That(snapshot.Url, Is.EqualTo("https://prinzip.su/flats/shartashpark/65040"));
    }
    

    [Test]
    public void Parse_PageWithoutJsonLd_ThrowsFlatNotFound()
    {
        var html = "<html><head><title>Нет разметки</title></head><body>7 711 200 ₽</body></html>";

        var exception = Assert.Throws<FlatNotFoundException>(
            () => _parser.Parse(html));

        Assert.That(exception!.Message, Does.Contain("application/ld+json"));
    }
    
    [Test]
    public void Parse_EmptyHtml_ThrowsFlatNotFound() =>
        Assert.Throws<FlatNotFoundException>(
            () => _parser.Parse(string.Empty));
}
