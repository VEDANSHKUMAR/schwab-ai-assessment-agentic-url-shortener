# ADR-005: Asynchronous analytics

## Decision
`System.Threading.Channels` + `BackgroundService` inside the API process.

## Reason
Removes analytics I/O from redirect latency without Kafka ops cost for the interview scope.

## Trade-offs
Not durable across crash; upgrade path: outbox → Azure Service Bus / Kafka.
