# MiniShop — микросервисы на .NET 8

Учебный, но «как в проде» проект для заявки на стажировку (.NET Backend).

Стек: **C# / .NET 8, ASP.NET Core (Minimal API), EF Core + PostgreSQL, Kafka, Redis, gRPC, ClickHouse, Docker Compose, Serilog, HealthChecks**.

## Что делает

`Orders.Api` — заказы: создаёт заказ в PostgreSQL (транзакция + Outbox), отдаёт по id с кэшем в Redis, публикует событие в Kafka, отдаёт gRPC для внутренних вызовов.

`Analytics.Worker` — читает Kafka `orders.events`, пишет батчами в ClickHouse, отдаёт REST + gRPC витрину (`/stats/orders-by-day`, `/stats/top-products`).

## Архитектура

```
Client -> Orders.Api :8080 (REST + gRPC :8081)
  Orders.Api -> PostgreSQL :5432 (orders + outbox в одной транзакции)
  Orders.Api -> Redis :6379 (cache-aside, TTL 5 мин, инвалидация при записи)
  Orders.Api -> Kafka :9092 (topic orders.events, key=orderId)
  OutboxPublisher (BackgroundService в Orders.Api) -> Kafka (at-least-once, идемпотентный consumer)

Kafka orders.events -> Analytics.Worker
  Analytics.Worker -> ClickHouse :8123 (таблица order_events, MergeTree)
  Client -> Analytics.Worker :8082 (REST витрина) + gRPC
```

Почему так (что говорить на собесе):
- **Outbox вместо dual-write**: заказ и событие коммитим одной транзакцией в Postgres, отдельный воркер доставляет в Kafka. Нет потери событий при падении между DB и Kafka.
- **Cache-aside + инвалидация**: чтение `GET /orders/{id}` сначала Redis, затем Postgres. Запись инвалидирует ключ. Штампede защищён коротким TTL, а не lock-ом — осознанный трейд-офф для стажировки.
- **Kafka key = orderId**: порядок событий одного заказа гарантирован внутри партиции.
- **ClickHouse отдельно от Postgres**: OLTP (заказы) и OLAP (аналитика) разведены. В ClickHouse только append-only события, агрегации считаются на лету.
- **gRPC для sync между сервисами, Kafka для async**: sync — где нужен ответ сразу, async — где можно eventual consistency.
- **Result-паттерн вместо исключений** для бизнес-ошибок, глобальный `IExceptionHandler` — только для unexpected.

## Быстрый старт

```bash
docker compose up --build
# Orders REST:  http://localhost:8080/swagger
# Orders gRPC:  localhost:8081
# Analytics REST: http://localhost:8082/stats/orders-by-day?days=7
# ClickHouse UI: http://localhost:8123/play
```

Создать заказ:

```bash
curl -X POST http://localhost:8080/orders \
  -H 'Content-Type: application/json' \
  -d '{"customerId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","items":[{"sku":"coffee-250","qty":2,"price":349}]}'
```

Проверить аналитику (через ~5 сек после создания):

```bash
curl 'http://localhost:8082/stats/orders-by-day?days=7'
```

## Локальный запуск без Docker (нужен SDK 8 + инфра из compose)

```bash
docker compose up -d postgres redis kafka clickhouse
dotnet run --project src/Orders/Orders.Api
dotnet run --project src/Analytics/Analytics.Worker
```

## Структура (Clean Architecture, сжатая)

- `src/Shared/SharedKernel` — `Result<T>`, `Entity` (без зависимостей).
- `src/Orders/Orders.Domain` — чистый домен: `Order`, `Money`, `OrderItem`, доменные события. Ноль ссылок на EF/Kafka.
- `src/Orders/Orders.Infrastructure` — EF Core + Postgres, Outbox, Kafka-продюсер, Redis-кэш. Зависит от Domain.
- `src/Orders/Orders.Api` — Minimal API endpoints (тонкие), DI, healthchecks, gRPC-сервис. Зависит от Domain + Infrastructure.
- `src/Analytics/Analytics.Worker` — Kafka-consumer (`BackgroundService`), ClickHouse-repo с батчингом, REST-витрина.
- `proto/orders.proto` — контракт-first: один proto генерирует сервер и клиент.

## Технические решения

| Вопрос | Решение |
|---|---|
| Маппинг | Вручную через `static ToResponse()` — без AutoMapper, видно преобразование |
| Валидация | Тонкая ручная в handler + `ArgumentException`→ 400 через handler; без FluentValidation чтобы не тащить зависимость в pet-проект |
| Конфиг | `Options<T>` + `ValidateOnStart`, секреты только через env в compose |
| Логи | Serilog JSON в stdout, `TraceId` из `Activity` для связки Orders→Kafka→Analytics |
| Health | `/health` проверяет pg/redis/kafka/clickhouse, в compose есть `depends_on: condition: service_healthy` |
| Тесты | `tests/`: домен (`Order.Create`), регрессия кэша (`Rehydrate` сохраняет Id/Status), `Money.TryAdd`; handlers/consumer — моками (следующий шаг) |

## Контракт топика `orders.events`

Продюсер (`Orders.Api`, Outbox) и консьюмер (`Analytics.Worker`) дублируют схему осознанно — у каждого сервиса своя копия, как в проде (единый nuget-пакет = лишний coupling). Поля JSON:

| Поле | Тип | Комментарий |
|---|---|---|
| `eventId` | uuid | ключ идемпотентности в ClickHouse (`ReplacingMergeTree`) |
| `eventType` | string | сейчас всегда `order.created` |
| `orderId` | uuid | также Kafka key → порядок одного заказа в партиции |
| `customerId` | uuid | для будущих витрин по клиентам |
| `totalMinor` | int64 | сумма в минорных единицах, без float |
| `currency` | string | ISO-4217 |
| `occurredAt` | datetime | время доменного события, партиция CH по месяцу |

## Что бы добавил дальше (честно для резюме)

- Пагинация + `AsNoTracking` в листинге, индекс `(created_at)`.
- Idempotency-Key на POST /orders.
- Schema Registry / Avro вместо JSON в Kafka.
- OpenTelemetry traces (OTLP → Jaeger).
