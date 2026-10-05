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

2. **Sign-in codes.** Decide where the six-digit code will appear:
   - With `Email__UserName` and `Email__Password` filled in `.env` (a Gmail App Password), the
     code is emailed to the real address. The API's startup log says
     `Sign-in codes will be emailed through smtp.gmail.com:587`.
   - With them blank, Development prints the code in the API console instead.
   - Restart the API after editing `.env`.

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
| 9 | **Sign in**: enter an email, read the code, enter it | "No passwords. A six-digit code, rate-limited, hashed in the database, and locked after five wrong tries." |
| 10 | Back on a trip, click **Request to join** | Honest answer: "Join requests and escrow payments are the next release." |
| 11 | Scroll to the footer: **API healthy** | "Live check: browser → API → SQL Server." |

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
