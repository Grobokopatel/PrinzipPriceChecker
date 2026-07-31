# Prinzip Price Checker

Сервис слежения за ценами квартир на [prinzip.su](https://prinzip.su): подписка по ссылке на
объявление и email, периодическая проверка цены, уведомления при её изменении.

## Стек

- .NET 10, ASP.NET Core Web API
- EF Core 10 + SQLite (миграции применяются при старте)
- MailKit для отправки писем по SMTP
- Swagger UI для ручной проверки (описания операций - из XML-комментариев)
- NUnit 4 - модульные тесты
- Запуск через Docker Compose (в комплекте Mailpit для просмотра писем)

## Быстрый старт

```bash
docker compose up --build
```

Swagger UI - http://localhost:8080/swagger или http://localhost:8080/
Входящие письма (Mailpit) - http://localhost:8025

База SQLite лежит в именованном томе `pricechecker-data` (`/app/data/pricechecker.db`),
поэтому подписки не теряются при перезапуске контейнера.

## Проверка сценария целиком

**1. Подписаться на квартиру**

```bash
curl -X POST http://localhost:8080/api/subscriptions \
  -H 'Content-Type: application/json' \
  -d '{"url":"https://prinzip.su/flats/shartashpark/65040/","email":"buyer@example.com"}'
```

Цена подтягивается с сайта сразу при подписке:

```json
{
  "id": 1,
  "email": "buyer@example.com",
  "flat": {
    "flatId": 1,
    "url": "https://prinzip.su/flats/shartashpark/65040",
    "name": "Квартира с кухней-гостиной и двумя комнатами",
    "description": "Шарташ Парк, 1 дом, 1 дом, кв. № 398",
    "currentPrice": 7711200,
    "currentPriceFormatted": "7 711 200 ₽"
  }
}
```

**2. Посмотреть актуальные цены со ссылками**

```bash
curl http://localhost:8080/api/flats
```

**3. Заменить сохранённую цену и получить письмо**

```bash
curl -X PUT http://localhost:8080/api/flats/1/price \
  -H 'Content-Type: application/json' \
  -d '{"newPrice": 5000000, "sendNotification": true}'
```

```json
{
  "oldPrice": 7711200,
  "newPrice": 5000000,
  "notificationsCount": 1,
  "error": null
}
```

С `"sendNotification": false` цена меняется без писем.
Если новая цена совпадает с сохранённой, не происходит ничего: ни записи в истории, ни уведомлений.

**4. Убедиться, что письмо ушло**

Письмо появится в Mailpit - http://localhost:8025. Тот же факт виден и в журнале сервиса:

```bash
curl http://localhost:8080/api/notifications
```

История цен по квартире:

```bash
curl http://localhost:8080/api/flats/1/history
```

Замена цены выполняется по квартире, а не по подписке: цена принадлежит квартире, и на одну
квартиру может быть подписано несколько почт. Нужный `flatId` возвращается в ответе на
подписку и в `GET /api/flats`.

Подписка на новую квартиру создаётся только если её цену удалось получить прямо во время
запроса - иначе мы не знаем, существует ли такая квартира вообще.

Если же квартира уже отслеживается (на неё подписан кто-то ещё), сайт при подписке не
опрашивается вовсе: её цена в базе поддерживается фоновой проверкой. Второй подписчик
оформляется мгновенно и не зависит от того, доступен ли сайт прямо сейчас.

Ссылки нормализуются, поэтому `.../65040/`, `.../65040` и `.../65040?utm_source=x` - одна и та
же квартира; на неё может быть подписано несколько адресов, страница при проверке
запрашивается один раз.

## Как отслеживается цена

- Цена берётся из разметки `<script type="application/ld+json">` на странице объявления:
  берётся первый узел `Product`, у которого заполнен `offers.price`.
- Фоновая служба `PriceMonitorWorker` периодически (по умолчанию раз в 10 минут, в Compose -
  раз в 5) обходит все квартиры.
- Если цена отличается от сохранённой, сервис пишет запись в историю, обновляет цену и
  отправляет письмо каждому подписчику.
- Первое получение цены (при подписке на ещё не отслеживаемую квартиру) письмом не
  сопровождается.
- Недоступность сайта не ломает фоновую работу: ошибка сохраняется в `lastCheckError`, цена
  остаётся прежней, следующая проверка пройдёт по расписанию. На оформление подписки это не
  распространяется - там недоступность сайта означает отказ.
- Ошибка отправки одного письма не мешает отправке остальных и попадает в журнал уведомлений.

## Настройки

Переопределяются переменными окружения:

| Переменная                       | По умолчанию                        | Описание                                    |
|----------------------------------|-------------------------------------|---------------------------------------------|
| `ConnectionStrings__Default`     | `Data Source=data/pricechecker.db`  | Строка подключения к SQLite                 |
| `Monitoring__Enabled`            | `true`                              | Включена ли фоновая проверка                |
| `Monitoring__Interval`           | `00:10:00`                          | Период обхода всех квартир                  |
| `Monitoring__StartupDelay`       | `00:00:10`                          | Задержка перед первой проверкой             |
| `Email__Provider`                | `log`                               | `smtp` - отправлять письма, `log` - в журнал |
| `Email__FromAddress`             | `noreply@pricechecker.local`        | Адрес отправителя                           |
| `Email__Smtp__Host`              | `localhost`                         | SMTP-сервер                                 |
| `Email__Smtp__Port`              | `1025`                              | Порт SMTP                                   |
| `Email__Smtp__UseStartTls`       | `false`                             | Использовать STARTTLS                       |
| `Email__Smtp__UserName`          | -                                   | Логин SMTP                                  |
| `Email__Smtp__Password`          | -                                   | Пароль SMTP                                 |

Провайдер `log` - режим по умолчанию: сервис полностью работоспособен без настройки почты,
письма пишутся в лог приложения и в журнал `GET /api/notifications`. В Compose включён `smtp`
с отправкой в Mailpit.

## Тесты

```bash
dotnet test tests/PrinzipPriceChecker.Tests/PrinzipPriceChecker.Tests.csproj
```

## Запуск без Docker

```bash
dotnet run --project src/PrinzipPriceChecker.Api
```

Сервис поднимется на порту из `launchSettings`/`ASPNETCORE_URLS`, база создастся в
`src/PrinzipPriceChecker.Api/data/pricechecker.db`.

## Структура

```
src/PrinzipPriceChecker.Api/
  Contracts/      DTO запросов и ответов
  Controllers/    Контроллеры HTTP API
  Data/           AppDbContext и миграции EF Core
  Domain/         TrackedFlat, Subscription, PriceChange, NotificationRecord
  Parsing/        Разбор JSON-LD и нормализация ссылок
  Services/       Мониторинг цен, подписки, ручная подмена цены, отправка писем, фоновая служба
  Validation/     Проверка email
```
