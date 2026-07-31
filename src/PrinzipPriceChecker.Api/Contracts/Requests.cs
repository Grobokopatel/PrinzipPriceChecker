namespace PrinzipPriceChecker.Api.Contracts;

/// <summary>Запрос на подписку по изменению цены квартиры.</summary>
/// <param name="Url">Ссылка на объявление, например https://prinzip.su/flats/shartashpark/65040/.</param>
/// <param name="Email">Email, на который присылать уведомления.</param>
public record CreateSubscriptionRequest(string? Url, string? Email);

/// <summary>Запрос на ручную установку сохранённой цены квартиры.</summary>
/// <param name="NewPrice">Новая сохранённая цена; должна быть больше нуля.</param>
/// <param name="SendNotification">Посылать ли уведомление о изменении цены на почту?</param>
public record SetPriceRequest(long NewPrice, bool SendNotification);
