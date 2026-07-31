using NUnit.Framework;
using PrinzipPriceChecker.Api.Parsing;

namespace PrinzipPriceChecker.Tests;

[TestFixture]
public class FlatUrlTests
{
    [TestCase("https://prinzip.su/flats/shartashpark/65040/")]
    [TestCase("https://prinzip.su/flats/shartashpark/65040")]
    [TestCase("http://prinzip.su/flats/shartashpark/65040/")]
    [TestCase("https://prinzip.su/flats/shartashpark/65040/?utm_source=test")]
    [TestCase("prinzip.su/flats/shartashpark/65040")]
    [TestCase("  https://prinzip.su/flats/shartashpark/65040/  ")]
    public void TryNormalize_EquivalentLinks_ProduceSameCanonicalUrl(string url)
    {
        var normalized = AssertNormalized(url);

        Assert.That(normalized, Is.EqualTo("https://prinzip.su/flats/shartashpark/65040"));
    }
    
    [TestCase(null, "Пустая")]
    [TestCase("", "Пустая")]
    [TestCase("   ", "Пустая")]
    [TestCase("https://example.com/flats/shartashpark/65040", "prinzip.su")]
    [TestCase("https://prinzip.su/", "Ссылка должна вести на страницу квартиры")]
    [TestCase("https://prinzip.su/flats/shartashpark", "Ссылка должна вести на страницу квартиры")]
    [TestCase("https://prinzip.su/flats/shartashpark/65040/extra", "Ссылка должна вести на страницу квартиры")]
    [TestCase("https://prinzip.su/news/65040", "Ссылка должна вести на страницу квартиры")]
    [TestCase("ftp://prinzip.su/flats/shartashpark/65040", "http/https")]
    public void TryNormalize_InvalidLinks_AreRejectedWithExplanation(string? url, string expectedHint)
    {
        var result = FlatUrl.TryNormalize(url, out var normalized, out var error);

        Assert.That(result, Is.False);
        Assert.That(normalized, Is.Null);
        Assert.That(error, Does.Contain(expectedHint));
    }
    
    private static string AssertNormalized(string url)
    {
        Assert.That(FlatUrl.TryNormalize(url, out var normalized, out var error), Is.True, error);

        return normalized!;
    }
}
