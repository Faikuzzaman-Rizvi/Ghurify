# Go-live checklist

Tick every line on **production** before the first real booking. Each line says how to check it.

## Configuration

- [ ] `GET https://api.ghurify.app/api/v1/health/ready` answers `Healthy` with storage `Healthy` and email `Configured`.
- [ ] `ASPNETCORE_ENVIRONMENT=Production` on the API and the worker (the API refuses the fake payment gateway and development secrets there).
- [ ] Every secret comes from Key Vault, is 32+ random characters, and differs from staging.
- [ ] `Payments__Provider=sslcommerz` with live credentials and `SslCommerz:Sandbox=false`.
- [ ] `Cors__AllowedOrigins__0` and `Storage__AllowedOrigins__0` are exactly the web origin.
- [ ] The API is a subdomain of the web domain (the sign-in cookie is same-site only).
- [ ] The jobs worker has `Jobs__RunServer=true` and **one** instance; the API has `Jobs__RunServer=false`.
- [ ] `Ekyc__Provider` is empty (manual review) or a real, contracted provider. Never `fake`.

## Data

- [ ] Destinations and emergency points are seeded; the safety desk has **checked every emergency point's phone and position** (Admin → Emergency points shows none unchecked).
- [ ] No demo data: no account ends in `@demo.ghurify.app` (`SELECT COUNT(*) FROM Main.[User] WHERE Email LIKE '%@demo.ghurify.app'` is 0).
- [ ] The first admin exists (RUNBOOK, "Make someone an admin"), and at least two people hold SafetyDesk.
- [ ] Point-in-time restore is on for the database, and someone has done one restore drill.

## Money

- [ ] One real end-to-end payment of a small amount on a test trip, then a cancellation: the refund arrives.
- [ ] The SSLCommerz IPN URL points at `https://api.ghurify.app/api/v1/payments/webhooks/sslcommerz`.
- [ ] The finance desk knows how to approve payouts (Admin → Payouts) and read a booking's escrow (Admin → Bookings & payments).

## Safety

- [ ] SMS provider chosen and wired for emergency contacts (until then the desk calls them; the SOS screen says so).
- [ ] SOS board staffed during trip hours, with a written rota and phone numbers.
- [ ] An SOS drill on staging: raise, acknowledge, resolve, with the desk watching live.

## Security

- [ ] `npm audit --omit=dev` and `dotnet list package --vulnerable` show nothing high or critical.
- [ ] The web app's CSP has run in report-only mode on staging with no violations; switch `Content-Security-Policy-Report-Only` to `Content-Security-Policy` in `staticwebapp.config.json`.
- [ ] Logs are checked for a day of staging traffic: no email addresses, phone numbers, codes or ID numbers in clear.
- [ ] Front Door / App Service access logs do not keep query strings (the realtime connection carries its token there).

## People

- [ ] Privacy notice and terms published in Bangla and English, including the 30-day deletion of ID photos.
- [ ] Support email monitored; the runbook has been read by everyone on call.
- [ ] Pilot hosts briefed: payouts in stages, check-ins, SOS.
