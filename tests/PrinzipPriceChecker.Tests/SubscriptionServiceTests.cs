using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using PrinzipPriceChecker.Api.Services;
using PrinzipPriceChecker.Tests.Fakes;

namespace PrinzipPriceChecker.Tests;

[TestFixture]
public class SubscriptionServiceTests
{
    private const string FlatUrl = "https://prinzip.su/flats/shartashpark/65040";

    private TestDatabase _database = null!;
    private FakePriceSource _priceSource = null!;
    private StubTimeProvider _timeProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _database = new TestDatabase();
        _priceSource = new FakePriceSource();
        _timeProvider = new StubTimeProvider(new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero));
    }

    [TearDown]
    public void TearDown() => _database.Dispose();

    [Test]
    public async Task Create_NewFlat_SavesSubscriptionWithPriceFromSite()
    {
        _priceSource.SetPrice(FlatUrl, 7_711_200);

        var outcome = await CreateService().CreateAsync(FlatUrl, "buyer@example.com", CancellationToken.None);

        Assert.That(outcome.IsCreated, Is.True);
        Assert.That(outcome.Subscription, Is.Not.Null);
        Assert.That(outcome.Subscription!.Email, Is.EqualTo("buyer@example.com"));

        await using var assertContext = _database.CreateContext();

        var flat = await assertContext.TrackedFlats.SingleAsync(CancellationToken.None);
        Assert.That(flat.Url, Is.EqualTo(FlatUrl));
        Assert.That(flat.CurrentPrice, Is.EqualTo(7_711_200));
        Assert.That(flat.Description, Is.EqualTo(FakePriceSource.DefaultDescription));
        Assert.That(flat.LastCheckError, Is.Null);

        var entry = await assertContext.PriceHistory.SingleAsync(CancellationToken.None);
        Assert.That(entry.Price, Is.EqualTo(7_711_200));
    }

    [Test]
    public async Task Create_SecondEmailForSameFlat_ReusesFlatAndDoesNotRefetchHistory()
    {
        _priceSource.SetPrice(FlatUrl, 7_711_200);

        var service = CreateService();
        await service.CreateAsync(FlatUrl, "buyer@example.com", CancellationToken.None);
        var second = await service.CreateAsync(FlatUrl, "agent@example.com", CancellationToken.None);

        Assert.That(second.IsCreated, Is.True);

        await using var assertContext = _database.CreateContext();

        Assert.That(await assertContext.TrackedFlats.CountAsync(CancellationToken.None), Is.EqualTo(1));
        Assert.That(await assertContext.Subscriptions.CountAsync(CancellationToken.None), Is.EqualTo(2));
        Assert.That(await assertContext.PriceHistory.CountAsync(CancellationToken.None), Is.EqualTo(1));
    }

    [Test]
    public async Task Create_SecondEmailForSameFlat_DoesNotAskSite()
    {
        _priceSource.SetPrice(FlatUrl, 7_711_200);

        var service = CreateService();
        await service.CreateAsync(FlatUrl, "buyer@example.com", CancellationToken.None);
        _priceSource.ResetRequestCount();

        await service.CreateAsync(FlatUrl, "agent@example.com", CancellationToken.None);

        Assert.That(_priceSource.RequestCount, Is.Zero);
    }

    [Test]
    public async Task Create_SecondEmailForSameFlat_SucceedsWhileSiteIsDown()
    {
        _priceSource.SetPrice(FlatUrl, 7_711_200);

        var service = CreateService();
        await service.CreateAsync(FlatUrl, "buyer@example.com", CancellationToken.None);

        _priceSource.SetFailure(FlatUrl, "Не удалось загрузить страницу объявления: таймаут.");

        var second = await service.CreateAsync(FlatUrl, "agent@example.com", CancellationToken.None);

        Assert.That(second.IsCreated, Is.True);

        await using var assertContext = _database.CreateContext();

        var flat = await assertContext.TrackedFlats.SingleAsync(CancellationToken.None);
        Assert.That(flat.CurrentPrice, Is.EqualTo(7_711_200));
        Assert.That(await assertContext.Subscriptions.CountAsync(CancellationToken.None), Is.EqualTo(2));
    }

    [Test]
    public async Task Create_SameEmailTwice_ReportsExistingSubscription()
    {
        _priceSource.SetPrice(FlatUrl, 7_711_200);

        var service = CreateService();
        await service.CreateAsync(FlatUrl, "buyer@example.com", CancellationToken.None);
        var duplicate = await service.CreateAsync(FlatUrl, "BUYER@example.com", CancellationToken.None);

        Assert.That(duplicate.IsCreated, Is.False);

        await using var assertContext = _database.CreateContext();
        Assert.That(await assertContext.Subscriptions.CountAsync(CancellationToken.None), Is.EqualTo(1));
    }

    [Test]
    public async Task Create_SiteUnavailable_DoesNotCreateSubscription()
    {
        _priceSource.SetFailure(FlatUrl, "Не удалось загрузить страницу объявления: таймаут.");

        var outcome = await CreateService().CreateAsync(FlatUrl, "buyer@example.com", CancellationToken.None);

        Assert.That(outcome.IsCreated, Is.False);
        Assert.That(outcome.Status, Is.EqualTo(CreateSubscriptionStatus.SiteUnavailable));
        Assert.That(outcome.Error, Does.Contain("таймаут"));

        await AssertNothingSavedAsync();
    }

    [Test]
    public async Task Create_FlatDoesNotExistOnSite_DoesNotCreateSubscription()
    {
        _priceSource.SetNotFound(FlatUrl);

        var outcome = await CreateService().CreateAsync(FlatUrl, "buyer@example.com", CancellationToken.None);

        Assert.That(outcome.IsCreated, Is.False);
        Assert.That(outcome.Status, Is.EqualTo(CreateSubscriptionStatus.FlatNotFound));
        Assert.That(outcome.Error, Does.Contain("404"));

        await AssertNothingSavedAsync();
    }

    private async Task AssertNothingSavedAsync()
    {
        await using var assertContext = _database.CreateContext();

        Assert.That(await assertContext.Subscriptions.CountAsync(CancellationToken.None), Is.Zero);
        Assert.That(await assertContext.TrackedFlats.CountAsync(CancellationToken.None), Is.Zero);
        Assert.That(await assertContext.PriceHistory.CountAsync(CancellationToken.None), Is.Zero);
    }

    private SubscriptionService CreateService() => new(
        _database.Context,
        _priceSource,
        _timeProvider,
        NullLogger<SubscriptionService>.Instance);
}
