# Minishop

Small microservices pet project in .NET 8. Orders service with Postgres outbox to Kafka, Redis cache and gRPC, plus an analytics worker that sinks events into ClickHouse.

Made as a portfolio piece for a backend internship application.

## Stack

- .NET 8, ASP.NET Core Minimal API, EF Core, gRPC
- PostgreSQL 16, Redis 7, Kafka (KRaft mode), ClickHouse
- Serilog, HealthChecks, Docker Compose

## Services

- `Orders.Api` (`:8080` REST, `:8081` gRPC). Creates orders in Postgres, serves reads with a Redis cache, publishes `order.created` to Kafka through a transactional outbox.
- `Analytics.Worker` (`:8082`). Consumes `orders.events`, appends them to ClickHouse, exposes `/stats/orders-by-day`.

## Run

```bash
docker compose up --build
```

Create an order:

```bash
curl -X POST http://localhost:8080/orders \
  -H 'Content-Type: application/json' \
  -d '{"customerId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","items":[{"sku":"coffee-250","qty":2,"price":349}]}'
```

Check the stats after a few seconds:

```bash
curl 'http://localhost:8082/stats/orders-by-day?days=7'
```

Swagger: `http://localhost:8080/swagger`. Health: `/health` on both services.

Local run without Docker images (infra still from compose):

```bash
docker compose up -d postgres redis kafka clickhouse
dotnet run --project src/Orders/Orders.Api
dotnet run --project src/Analytics/Analytics.Worker
```

## How it works

1. `POST /orders` inserts the order and an outbox row in one Postgres transaction. No dual write to Kafka.
2. A background publisher sends unsent outbox rows to topic `orders.events` with key = order id, so events of one order stay ordered in a partition.
3. `GET /orders/{id}` checks Redis first, falls back to Postgres on miss. Delivery is at-least-once, so the analytics side dedups on `event_id`.
4. The worker batches events into ClickHouse table `order_events` (`ReplacingMergeTree`, partitioned by month) and aggregates stats with plain SQL.

Kafka message fields: `eventId`, `eventType`, `orderId`, `customerId`, `totalMinor`, `currency`, `occurredAt`. Both services keep their own copy of the schema. The gRPC contract lives in `proto/orders.proto` and covers sync order lookups between services.

## Layout

```
proto/                      gRPC contract
src/Shared/SharedKernel     Result type, Entity base
src/Orders/Orders.Domain    Order aggregate, value objects, domain events
src/Orders/Orders.Infrastructure  EF mappings, outbox, Redis decorator, Kafka producer
src/Orders/Orders.Api       endpoints, gRPC service, DI, healthchecks
src/Analytics/Analytics.Worker    Kafka consumer, ClickHouse sink, stats API
tests/Domain.Tests          unit tests for the domain
```

## Notes

Dev passwords are hardcoded in `docker-compose.yml`. There is no auth. This is demo code, not production ready. Things I would add next: idempotency keys on `POST /orders`, keyset pagination, Avro with schema registry, OpenTelemetry traces.
