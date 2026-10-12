# Minishop

Небольшой пет-проект на микросервисах в .NET 8. Сервис заказов с outbox в Postgres, Kafka, кэшем в Redis и gRPC, плюс воркер аналитики, который складывает события в ClickHouse.

Сделан как портфолио для заявки на стажировку backend-разработчиком.

## Стек

- .NET 8, ASP.NET Core Minimal API, EF Core, gRPC
- PostgreSQL 16, Redis 7, Kafka (режим KRaft), ClickHouse
- Serilog, HealthChecks, Docker Compose

## Сервисы

- `Orders.Api` (`:8080` REST, `:8081` gRPC). Создает заказы в Postgres, отдает чтение через кэш в Redis, публикует `order.created` в Kafka через транзакционный outbox.
- `Analytics.Worker` (`:8082`). Читает `orders.events`, дописывает их в ClickHouse, отдает `/stats/orders-by-day`.

## Запуск

```bash
docker compose up --build
```

Создать заказ:

```bash
curl -X POST http://localhost:8080/orders \
  -H 'Content-Type: application/json' \
  -d '{"customerId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","items":[{"sku":"coffee-250","qty":2,"price":349}]}'
```

Проверить статистику через несколько секунд:

```bash
curl 'http://localhost:8082/stats/orders-by-day?days=7'
```

Swagger: `http://localhost:8080/swagger`. Healthcheck: `/health` на обоих сервисах.

Запуск без Docker-образов (инфра все равно из compose):

```bash
docker compose up -d postgres redis kafka clickhouse
dotnet run --project src/Orders/Orders.Api
dotnet run --project src/Analytics/Analytics.Worker
```

## Как устроено

1. `POST /orders` кладет заказ и строку outbox в одной транзакции Postgres. Двойной записи в Kafka нет.
2. Фоновый паблишер отправляет неотправленные строки outbox в топик `orders.events` с ключом = id заказа, поэтому события одного заказа лежат в партиции по порядку.
3. `GET /orders/{id}` сначала смотрит в Redis, при промахе идет в Postgres. Доставка at-least-once, поэтому сторона аналитики дедуплицирует по `event_id`.
4. Воркер пачками пишет события в таблицу ClickHouse `order_events` (`ReplacingMergeTree`, партиции по месяцам), статистику считает обычным SQL.

Поля сообщения в Kafka: `eventId`, `eventType`, `orderId`, `customerId`, `totalMinor`, `currency`, `occurredAt`. У каждого сервиса своя копия схемы. gRPC-контракт лежит в `proto/orders.proto` и покрывает синхронные запросы заказов между сервисами.

## Структура

```
proto/                      gRPC-контракт
src/Shared/SharedKernel     тип Result, базовый Entity
src/Orders/Orders.Domain    агрегат Order, value objects, доменные события
src/Orders/Orders.Infrastructure  маппинги EF, outbox, Redis-декоратор, Kafka-продюсер
src/Orders/Orders.Api       эндпоинты, gRPC-сервис, DI, healthcheck
src/Analytics/Analytics.Worker    Kafka-консьюмер, запись в ClickHouse, API статистики
tests/Domain.Tests          юнит-тесты домена
```

## Замечания

Пароли для разработки зашиты в `docker-compose.yml`. Авторизации нет. Это демо-код, не продакшн. Что добавил бы дальше: idempotency-ключи на `POST /orders`, keyset-пагинацию, Avro со schema registry, трейсы OpenTelemetry.
