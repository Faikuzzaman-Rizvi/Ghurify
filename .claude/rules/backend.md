---
paths:
  - "src/Ghurify.Api/**"
  - "src/Ghurify.Application/**"
  - "src/Ghurify.Domain/**"
  - "src/Ghurify.Infrastructure/**"
---

# Backend rules

## Structure

- Feature folders in every project: `Identity, Trips, Bookings, Payments, Marketplace, Social,
  Chat, Safety, Notifications, Admin`.
- Endpoints: Minimal API groups per feature in `Ghurify.Api/Endpoints/{Feature}Endpoints.cs`,
  mapped under `/api/v1/{feature}`. Keep endpoints thin: bind, call the use case, map the result.
- Use cases: one class per action in `Ghurify.Application/{Feature}/{Action}Handler.cs`
  (e.g. `CreateTripHandler`), registered in DI. No MediatR.
- Domain entities hold invariants (e.g. `Trip.ReserveSeat()` refuses when full). No framework
  types in Domain.
- Records for DTOs and requests. Methods with 5+ parameters take a record.
- Do not publish a type that has one consumer; nest it privately.

## Errors and results

- Expected failures (not found, not allowed, validation, conflict) return `ProblemDetails` with the
  right status: 400, 401, 403, 404, 409, 422. Unexpected exceptions go to the global handler.
- Validation with FluentValidation at the API boundary; domain rules in the entity.

## Security

- `[Authorize]` by default; opt out explicitly with `AllowAnonymous`.
- Roles: Traveler, Host, Creator, Celebrity, Guide, Operator, Partner, Moderator, SafetyDesk, Admin.
  Use policies (`VerifiedHost`, `AdminOnly`) rather than role strings in endpoints.
- Ownership check in the handler **and** owner filter in SQL.
- Built-in rate limiter on auth, join-request and payment endpoints.
- Never log OTPs, tokens, NID numbers or full phone numbers.

## Payments and money (high-risk area: plan first, test hard)

- `IPaymentGateway` abstraction; providers: SSLCommerz, bKash, Nagad. Sandbox in non-production.
- Every payment call carries an idempotency key. Webhooks verify the signature, are idempotent,
  and are processed once (store provider transaction id with a unique index).
- Escrow is a double-entry ledger in `Pay.EscrowLedger`: Hold on payment, Release in stages
  (example 40% before departure, 60% after trip start), Refund on closure or host failure.
  The ledger must always balance per booking; a test asserts this.
- Seat holds expire after 30 minutes unpaid (Hangfire job).

## Real-time and jobs

- SignalR hubs: `/hubs/chat`, `/hubs/notify`, `/hubs/safety`. Authorize every hub method.
- Hangfire jobs live in `Ghurify.Infrastructure/Jobs/{Feature}`. Payloads carry ids only.
  Jobs are idempotent: re-running must not double-charge, double-release or double-notify.

## Config

- Bind options classes with `ValidateOnStart()` for required settings.
- `appsettings*.json` set to `CopyToPublishDirectory=Never` with a comment explaining why.
