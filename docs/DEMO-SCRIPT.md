# Showcase demo script

A 10-minute walk through what Ghurify does today. Every step below works against the `ras-x2`
database with the demo trips loaded.

## Before the audience arrives (5 minutes)

1. **Database.** If it has not been done on this database yet:

   ```powershell
   .\Publish-Database.ps1 -Demo
   ```

   This runs DbUp pre → dacpac → DbUp data → demo trips. Running it again changes nothing. The
   demo trips are dated relative to the day they were loaded, so reload them on a fresh database
   if the showcase is weeks later.

2. **Accounts.** Every demo account (`@demo.ghurify.app`, loaded with `-- demo`) signs in with
   the password `Ghurify-demo-2026`; for example `admin@demo.ghurify.app` (Admin) and
   `safety@demo.ghurify.app` (safety desk). A new account needs the six-digit code once, to
   confirm its email address, and "Forgot password?" sends one too. Decide where it will appear:
   - With `Email__UserName` and `Email__Password` filled in `.env` (a Gmail App Password), the
     code is emailed to the real address. The API's startup log says
     `Account emails (confirmation and password reset codes) will be sent through smtp.gmail.com:587`.
   - With them blank, Development prints the code in the API console instead.
   - Restart the API after editing `.env`.

   **Payments** go through the **SSLCommerz sandbox** in Development, with its public test store
   (no setup, needs internet). The traveller who pays needs a mobile number on their profile.

3. **Start it.** The API from Visual Studio (or `dotnet run --project src/Ghurify.Api`), then
   `cd web/ghurify-web; npm run dev`, and open <http://localhost:5173>.

4. **Reset the tour** so it plays on first load: in the browser console run
   `localStorage.removeItem('ghurify.tourSeen')`, or just click **Take the tour** when you want it.

## The walkthrough

| # | Do | Say |
| --- | --- | --- |
| 1 | Open the home page and let the **guided tour** run | "A one-minute tour introduces the product to first-time visitors, in Bangla or English." |
| 2 | Switch **বাং / EN** in the header | "Bangla first. Every string, price and date switches, including ৳ and Bangla digits." |
| 3 | Point at the destination cards, then **Saint Martin's Island** | "Each destination carries a live safety status. Saint Martin's is on *Caution*, and that notice follows every trip there." |
| 4 | Use the hero search: Sylhet, **Women only** → Search | "Search is server-side, in one stored procedure, and the filters live in the URL so a search can be shared." |
| 5 | On the explore page, change **Sort by** and **Budget** | "Only live trips appear. Drafts never do." |
| 6 | Open **Sajek sunrise weekend** | "Before anyone commits: seats left, the day-by-day plan, and **where every taka goes**. The lines add up exactly to the price, with no hidden fees." |
| 7 | Point at the ticket card's **escrow** note | "Payments will go into escrow and be released to the host in stages." |
| 8 | Click the 📍 destination chip → destination page with the map | "Every destination has its own page and map." |
| 9 | **Create an account**: name, email, password, then the code from the email | "The email is confirmed once with a code. After that it is just email and password: salted slow hashes, and five wrong passwords pause sign-in for that address, whether or not it has an account." |
| 10 | Back on a trip, click **Request to join**; as the host (`rafiq.chowdhury@demo.ghurify.app`), **Approve and hold a seat** | "The host approves, and a seat is held for 30 minutes while the traveller pays." |
| 11 | As the traveller: **My trips → Pay**, then **Pay** on the checkout | "Price, our 2% fee and the total, before anything is charged. The money goes into escrow." |
| 12 | On the **SSLCommerz** page: card `4111 1111 1111 1111`, expiry `12/30`, CVV `111`, any name → **PAY**; on the OTP page, any code → **Success** | "This is SSLCommerz's real sandbox: the same hosted checkout, cards, bKash and Nagad as live, with no real money. Our server confirms every payment with SSLCommerz directly; it never trusts the browser." |
| 13 | Back on Ghurify: **You are going!** → open the group chat | "Paid into escrow, the seat is confirmed, and the group chat opens." |
| 14 | Scroll to the footer: **API healthy** | "Live check: browser → API → SQL Server." |

## Things worth knowing if asked

- **Women-only trips** are shown to women and to anyone whose gender is not known yet, and hidden
  from men, both in search and on the trip page (which answers 404, exactly like a missing trip).
- **Architecture:** React 19 + TypeScript, ASP.NET Core (.NET 10) minimal APIs, Dapper and stored
  procedures, SQL Server with the schema in an SSDT dacpac and data changes in DbUp, temporal
  history on users and trips.
- **Tests:** 129 unit, 40 integration (real SQL Server in Docker, built from the dacpac), 34
  frontend.

## If something goes wrong on stage

| Symptom | Fix |
| --- | --- |
| Footer says **API not reachable** | The API is not running, or cannot reach `ras-x2`. Check the API console. |
| No trips anywhere | Demo data not loaded on this database: `.\Publish-Database.ps1 -Demo`. |
| `429` on sign-in | The per-IP limit (10 a minute) or per-address limit (3 per 10 minutes). Use another address. |
| Map tiles blank | No internet. The rest of the app works offline; skip step 8. |
| Payment page does not open ("payment service is not answering") | No internet, or the SSLCommerz sandbox is down. Set `Payments__Provider=fake` in `.env` and restart the API: a built-in pretend page takes its place. |
| Back from SSLCommerz, the page keeps "confirming" | The browser could not reach the API at `Payments:ApiBaseUrl` (http://localhost:5199): run the API on that port. |
