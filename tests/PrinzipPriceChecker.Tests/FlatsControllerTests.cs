using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using PrinzipPriceChecker.Api.Contracts;
using PrinzipPriceChecker.Api.Controllers;
using PrinzipPriceChecker.Api.Services;
using PrinzipPriceChecker.Tests.Fakes;

namespace PrinzipPriceChecker.Tests;

[TestFixture]
public class FlatsControllerTests
{
    private const string FlatUrl = "https://prinzip.su/flats/shartashpark/65040";

    private const long OldPrice = 2_000_000;
    private const long NewPrice = 1_000_000;

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
    public async Task GetPriceHistory_ReturnsEntriesNewestFirst()
    {
        var flatId = await ArrangeFlatAsync(OldPrice, "buyer@example.com");

        _timeProvider.Advance(TimeSpan.FromHours(1));
        await SetPriceAsync(flatId, NewPrice, sendNotification: false);

        var result = await CreateController().GetPriceHistory(flatId, CancellationToken.None);

        var history = result.IsOk();

        Assert.That(history,Has.Length.EqualTo(2));
        Assert.That(history[^1].Id, Is.EqualTo(1));
        Assert.That(history[^1].Price, Is.EqualTo(OldPrice));
        Assert.That(history[^2].Id, Is.EqualTo(2));
        Assert.That(history[^2].Price, Is.EqualTo(NewPrice));
        Assert.That(history[^2].DetectedAt, Is.GreaterThan(history[1].DetectedAt));
    }

    [Test]
    public async Task GetPriceHistory_UnknownFlat_ReturnsNotFound()
    {
        var result = await CreateController().GetPriceHistory(4242, CancellationToken.None);

        Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task SetFlatPrice_ChangesPriceAndNotifiesSubscribers()
    {
        var flatId = await ArrangeFlatAsync(OldPrice, "buyer@example.com");

        var result = await SetPriceAsync(flatId, NewPrice, sendNotification: true);

        var response = result.IsOk();

        Assert.That(response.OldPrice, Is.EqualTo(OldPrice));
        Assert.That(response.NewPrice, Is.EqualTo(NewPrice));
        Assert.That(response.NotificationsCount, Is.EqualTo(1));

        var email = _emailSender.SentMessages.Single();
        Assert.That(email.To, Is.EqualTo("buyer@example.com"));
        Assert.That(email.TextBody, Does.Contain(PriceFormatter.Format(NewPrice)));
        Assert.That(email.TextBody, Does.Contain(PriceFormatter.Format(OldPrice)));

        // Так как обновили цену вручную, то сайт не должен был опрашиваться
        Assert.That(_priceSource.RequestCount, Is.Zero);

        await using var assertContext = _database.CreateContext();

        var flat = await assertContext.TrackedFlats.SingleAsync(CancellationToken.None);
        Assert.That(flat.CurrentPrice, Is.EqualTo(NewPrice));
        // Запись при подписке плюс запись о замене.
        Assert.That(await assertContext.PriceHistory.CountAsync(CancellationToken.None), Is.EqualTo(2));

        var notification = await assertContext.Notifications.SingleAsync(CancellationToken.None);
        Assert.That(notification.Email, Is.EqualTo("buyer@example.com"));
        Assert.That(notification.OldPrice, Is.EqualTo(OldPrice));
        Assert.That(notification.NewPrice, Is.EqualTo(NewPrice));
        Assert.That(notification.IsSent, Is.True);
    }

    [Test]
    public async Task SetFlatPrice_WithoutNotification_OnlySavesPrice()
    {
        var flatId = await ArrangeFlatAsync(OldPrice, "buyer@example.com");

        var result = await SetPriceAsync(flatId, NewPrice, sendNotification: false);

        var response = result.IsOk();

        Assert.That(response.NotificationsCount, Is.Zero);
        Assert.That(_emailSender.SentMessages, Is.Empty);
        Assert.That(_priceSource.RequestCount, Is.Zero);

        await using var assertContext = _database.CreateContext();

        var flat = await assertContext.TrackedFlats.SingleAsync(CancellationToken.None);
        Assert.That(flat.CurrentPrice, Is.EqualTo(NewPrice));
        Assert.That(await assertContext.PriceHistory.CountAsync(CancellationToken.None), Is.EqualTo(2));
    }

    [Test]
    public async Task SetFlatPrice_SamePriceAsSaved_ChangesNothing()
    {
        var flatId = await ArrangeFlatAsync(OldPrice, "buyer@example.com");

        var result = await SetPriceAsync(flatId, OldPrice, sendNotification: true);

        var response = result.IsOk();

        Assert.That(response.OldPrice, Is.EqualTo(OldPrice));
        Assert.That(response.NewPrice, Is.EqualTo(OldPrice));
        Assert.That(response.NotificationsCount, Is.Zero);
        Assert.That(_emailSender.SentMessages, Is.Empty);

        await using var assertContext = _database.CreateContext();

        // В истории осталась только запись, сделанная при подписке.
        Assert.That(await assertContext.PriceHistory.CountAsync(CancellationToken.None), Is.EqualTo(1));
    }
    
    [TestCase(0)]
    [TestCase(-1000)]
    public async Task SetFlatPrice_NonPositivePrice_ReturnsValidationProblem(long price)
    {
        var flatId = await ArrangeFlatAsync(OldPrice, "buyer@example.com");

        var result = await SetPriceAsync(flatId, price, true);

        var problem = result.IsValidationProblem();
        Assert.That(problem.Errors.Keys, Does.Contain("NewPrice"));
        Assert.That(problem.Status, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task SetFlatPrice_UnknownFlat_ReturnsNotFound()
    {
        var result = await SetPriceAsync(4242, NewPrice, true);

        Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
    }

    private Task<ActionResult<ManualPriceChangeResult>> SetPriceAsync(int flatId, long price, bool sendNotification) =>
        CreateController().SetFlatPrice(
            flatId,
            new SetPriceRequest(price, sendNotification),
            CancellationToken.None);

    private FlatsController CreateController()
    {
        var monitor = CreateMonitor();
        var manualPrice = new ManualPriceService(_database.Context, _timeProvider, monitor);

        return MvcTestServices.WithContext(
            new FlatsController(_database.Context, monitor, manualPrice));
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
