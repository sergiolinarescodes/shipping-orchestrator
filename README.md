# shipping-orchestrator

Central .NET 10 platform that brokers between many ecommerce platforms (Shopify, WooCommerce, custom REST) and many carriers (PostNL, future). Modular monolith with three deployable hosts (`PublicApi`, `PrivateApi`, `Worker`), CQRS read side split into `Operations` (internal business) and `Customer` (tenant-facing), Wolverine on AWS SQS+SNS for messaging and sagas, PostgreSQL (Aurora-compatible).

## Quick start

```bash
docker compose up -d
./scripts/dev-up.ps1   # or run each host with: dotnet run --project src/<HostName>
```

Public API: http://localhost:5101 (Scalar UI at `/scalar`)
Private API: http://localhost:5102
Jaeger: http://localhost:16686

## Test

```bash
dotnet test
```

## Structure

- `src/ShippingOrchestrator.Domain` — aggregates, value objects, domain events
- `src/ShippingOrchestrator.Application` — CQRS handlers, sagas, routing engine
- `src/ShippingOrchestrator.Infrastructure` — EF Core, Wolverine, AWS clients, telemetry
- `src/ShippingOrchestrator.Modules.Abstractions` — `IConnectorModule`, carrier/ecommerce contracts
- `src/ShippingOrchestrator.PublicApi` — tenant-facing host
- `src/ShippingOrchestrator.PrivateApi` — internal/admin host
- `src/ShippingOrchestrator.Worker` — Wolverine handlers + sagas + projections
- `src/ShippingOrchestrator.EcommerceConnectors.Shopify` — Shopify connector
- `src/ShippingOrchestrator.CarrierConnectors.PostNL` — PostNL mock carrier
- `src/ShippingOrchestrator.ReadModels.Abstractions` — read DTOs + queries
- `src/ShippingOrchestrator.ReadModels.Projections` — Wolverine subscribers
- `src/ShippingOrchestrator.ReadModels.Operations` — internal-business read schema
- `src/ShippingOrchestrator.ReadModels.Customer` — tenant-facing read schema

Adding a new connector: drop a new project under `src/`, implement `IConnectorModule`, `AddConnectorModule<T>()` in each host. No edits to Application or Infrastructure.

## License

Copyright (C) 2026 Sergio Linares Peralta.

This project is licensed under the **GNU Affero General Public License v3.0 (AGPL-3.0)**. See [LICENSE](./LICENSE) for the full text.

In short: you are free to study, modify and self-host this code, but if you make it available over a network (including SaaS deployments) you must release your modifications under the same license. Commercial use without that source-disclosure obligation requires a separate licence from the author. For commercial licensing enquiries, contact [sergiolinaresperalta@gmail.com](mailto:sergiolinaresperalta@gmail.com).

