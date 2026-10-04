---
paths:
  - "tests/**"
  - "web/ghurify-web/src/**/*.test.*"
  - "web/ghurify-web/e2e/**"
---

# Testing rules

- **Unit tests** (`tests/Ghurify.UnitTests`): domain rules, handlers with in-memory fakes, validators,
  `DatabaseProjectFileTests` (every `.sql` listed once in the .sqlproj). No database. No mocking
  framework; write small hand-made fakes.
- **Integration tests** (`tests/Ghurify.IntegrationTests`): Testcontainers SQL Server, deploy the
  dacpac with DacFx, run DbUp, then exercise repositories and endpoints via `WebApplicationFactory`.
  Clean up rows you create. Mark with a trait so CI can run them separately.
- CI must fail if the integration suite reports zero executed tests.
- **Payments**: test success, failure, timeout, duplicate webhook, and ledger balance.
- **Authorization**: every new endpoint has at least one forbidden/not-owner test.
- **Frontend**: Vitest + Testing Library for components and hooks; Playwright for the core journey
  (sign up -> find trip -> request -> pay (sandbox) -> chat).
- Test names describe behaviour: `ReserveSeat_WhenTripIsFull_Refuses`.
- Never delete or skip a failing test to make the build green. Fix the cause or ask.
