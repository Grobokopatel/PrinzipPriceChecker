using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using PrinzipPriceChecker.Api.Contracts;
using PrinzipPriceChecker.Api.Controllers;
using PrinzipPriceChecker.Api.Domain;
using PrinzipPriceChecker.Api.Services;
using PrinzipPriceChecker.Tests.Fakes;

namespace PrinzipPriceChecker.Tests;

[TestFixture]
public class PriceMonitorTests
{
    private const string FlatUrl = "https://prinzip.su/flats/shartashpark/65040";

    private const long OldPrice = 7_000_000;
    private const long NewPrice = 6_500_000;
    
    private TestDatabase _database = null!;
    private FakePriceSource _priceSource = null!;
    private FakeEmailSender _emailSender = null!;
    private StubTimeProvider _timeProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _database = new TestDatabase();
        _priceSource = new FakePriceSource();
        _emailSender = new FakeEmailSender();
        _timeProvider = new StubTimeProvider(new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero));
    }

    [TearDown]
    public void TearDown() => _database.Dispose();

    [Test]
    public async Task CheckFlat_PriceChanged_NotifiesSubscribersAndWritesHistory()
    {
        var flatId = await ArrangeFlatAsync(OldPrice, "buyer@example.com", "agent@example.com");
        _priceSource.SetPrice(FlatUrl, NewPrice);

        var result = await CreateMonitor().CheckFlatAsync(flatId, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.PriceChanged, Is.True);
        Assert.That(result.OldPrice, Is.EqualTo(OldPrice));
        Assert.That(result.NewPrice, Is.EqualTo(NewPrice));
        Assert.That(result.NotificationsSent, Is.EqualTo(2));

        Assert.That(_emailSender.SentMessages, Has.Count.EqualTo(2));
        Assert.That(
            _emailSender.SentMessages.Select(message => message.To),
            Is.EquivalentTo(["buyer@example.com", "agent@example.com"]));

        var email = _emailSender.SentMessages[0];
        Assert.That(email.Subject, Does.Contain("снизилась"));
        Assert.That(email.TextBody, Does.Contain(PriceFormatter.Format(OldPrice)));
        Assert.That(email.TextBody, Does.Contain(PriceFormatter.Format(NewPrice)));
        Assert.That(email.TextBody, Does.Contain(PriceFormatter.Format(OldPrice - NewPrice)));
        Assert.That(email.TextBody, Does.Contain(FlatUrl));

        await using var assertContext = _database.CreateContext();

        var flat = await assertContext.TrackedFlats.SingleAsync(CancellationToken.None);
        Assert.That(flat.CurrentPrice, Is.EqualTo(NewPrice));
        Assert.That(flat.LastPriceChangeAt, Is.EqualTo(_timeProvider.Now));
        Assert.That(flat.LastCheckedAt, Is.EqualTo(_timeProvider.Now));
        Assert.That(flat.LastCheckError, Is.Null);

        var history = await assertContext.PriceHistory
            .OrderBy(entry => entry.Id)
            .ToListAsync(CancellationToken.None);

        Assert.That(history, Has.Count.EqualTo(2));
        Assert.That(history[^1].Price, Is.EqualTo(NewPrice));

        var notifications = await assertContext.Notifications.ToListAsync(CancellationToken.None);
        Assert.That(notifications, Has.Count.EqualTo(2));
        Assert.That(notifications, Has.All.Matches<NotificationRecord>(notification => notification.IsSent));
        Assert.That(notifications, Has.All.Matches<NotificationRecord>(notification => notification.Error == null));
    }

    [Test]
    public async Task CheckFlat_PriceUnchanged_DoesNotNotify()
    {
        var flatId = await ArrangeFlatAsync(OldPrice, "buyer@example.com");
        _priceSource.SetPrice(FlatUrl, OldPrice);

        var result = await CreateMonitor().CheckFlatAsync(flatId, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.PriceChanged, Is.False);
        Assert.That(result.NotificationsSent, Is.Zero);
        Assert.That(_emailSender.SentMessages, Is.Empty);

        await using var assertContext = _database.CreateContext();
        Assert.That(await assertContext.PriceHistory.CountAsync(CancellationToken.None), Is.EqualTo(1));
        Assert.That(assertContext.Notifications, Is.Empty);
    }

    [Test]
    public async Task CheckFlat_SiteUnavailable_KeepsPriceAndStoresError()
    {
        var flatId = await ArrangeFlatAsync(OldPrice, "buyer@example.com");
        _priceSource.SetFailure(FlatUrl, "Сайт вернул неуспешный ответ: HTTP 503.");

        var result = await CreateMonitor().CheckFlatAsync(flatId, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Success, Is.False);
        Assert.That(result.PriceChanged, Is.False);
        Assert.That(result.Error, Is.EqualTo("Сайт вернул неуспешный ответ: HTTP 503."));
        Assert.That(_emailSender.SentMessages, Is.Empty);

        await using var assertContext = _database.CreateContext();

        var flat = await assertContext.TrackedFlats.SingleAsync(CancellationToken.None);
        Assert.That(flat.CurrentPrice, Is.EqualTo(OldPrice));
        Assert.That(flat.LastCheckError, Is.EqualTo("Сайт вернул неуспешный ответ: HTTP 503."));
        Assert.That(flat.LastCheckedAt, Is.EqualTo(_timeProvider.Now));
    }
    
    [Test]
    public async Task CheckFlat_UnknownFlat_ReturnsNull()
    {
        var result = await CreateMonitor().CheckFlatAsync(flatId: 4242, CancellationToken.None);

        Assert.That(result, Is.Null);
    }
    
    [Test]
    public async Task CheckFlat_RepeatedCheckAfterChange_DoesNotNotifyTwice()
    {
        var flatId = await ArrangeFlatAsync(OldPrice, "buyer@example.com");
        _priceSource.SetPrice(FlatUrl, NewPrice);

        var monitor = CreateMonitor();

        await monitor.CheckFlatAsync(flatId, CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromMinutes(10));
        var secondCheck = await monitor.CheckFlatAsync(flatId, CancellationToken.None);

        Assert.That(secondCheck, Is.Not.Null);
        Assert.That(secondCheck!.PriceChanged, Is.False);
        Assert.That(_emailSender.SentMessages, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task CheckFlat_UpdatesNameAndDescriptionFromSite()
    {
        var flatId = await ArrangeFlatAsync(OldPrice, "buyer@example.com");
        _priceSource.SetPrice(FlatUrl, OldPrice, "Новое название", "Новое описание");

        await CreateMonitor().CheckFlatAsync(flatId, CancellationToken.None);

        await using var assertContext = _database.CreateContext();

        var flat = await assertContext.TrackedFlats.SingleAsync(CancellationToken.None);
        Assert.That(flat.Name, Is.EqualTo("Новое название"));
        Assert.That(flat.Description, Is.EqualTo("Новое описание"));
    }

    private PriceMonitor CreateMonitor() => new(
        _database.Context,
        _priceSource,
        _emailSender,
        _timeProvider,
        NullLogger<PriceMonitor>.Instance);

    /// <summary>
    /// Готовит квартиру тем же путём, каким её создаёт пользователь - подпиской через контроллер.
    /// Цену на этапе подписки отдаёт поддельный сайт.
    /// </summary>
    private async Task<int> ArrangeFlatAsync(long price, params string[] emails)
    {
        _priceSource.SetPrice(FlatUrl, price);

        var flatId = 0;

        foreach (var email in emails)
        {
            var result = await CreateSubscriptionsController().CreateSubscription(
                new CreateSubscriptionRequest(FlatUrl, email),
                CancellationToken.None);

            flatId = result.IsCreated().Flat.FlatId;
        }

        // Обнуляем счётчик, чтобы проверки ниже видели только запросы проверяемого кода.
        _priceSource.ResetRequestCount();

        return flatId;
    }

    private SubscriptionsController CreateSubscriptionsController() =>
        MvcTestServices.WithContext(
            new SubscriptionsController(
                _database.Context,
                new SubscriptionService(
                    _database.Context,
                    _priceSource,
                    _timeProvider,
                    NullLogger<SubscriptionService>.Instance)));
}
