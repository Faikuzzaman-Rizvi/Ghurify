# Ghurify: UI/UX redesign prompts (Airbnb as the reference)

Copy one prompt at a time into Claude Code, from the repository root, the same way as
`docs/PROMPTS.md`: plan mode first, approve the plan, let it implement, check the summary.

**Run D0 and D1 first.** Every page prompt after them assumes the design system and app shell
exist. After that, the page prompts can run in any order; the suggested order is the traveller's
path (D2 -> D17b), then hosting (D18 -> D21), then the admin desk (D22 -> D27).

Every prompt was written against the code and the generated OpenAPI types
(`web/ghurify-web/src/api/schema.d.ts`) as of 2026-10-05. Field names in the prompts are the real
field names. If `npm run gen:api` changes a type, trust the type over the prompt.

---

## The reference: what "like Airbnb" means here

Airbnb is the reference for **layout, hierarchy, interaction patterns and visual restraint**. It
is not a brand to copy. Never use Airbnb's name, logo, Rausch red (#FF385C), the Cereal typeface,
its illustrations or its copy. Ghurify keeps its own palette, fonts and Bangla-first voice.

What to take from Airbnb:

1. **Quiet canvas.** White page, near-black text, grey secondary text, 1px hairline dividers
   between sections instead of boxed cards. Colour is rare, so the one coloured button wins.
2. **The visual sells.** Large rounded-corner media tiles (12px radius) carry every card. Ghurify
   has no photos for trips or destinations, so the `Scenery` illustration fills Airbnb's photo
   slot at the same size, ratio and radius.
3. **Pill search.** A compact "Where · When · Who" pill in the header that expands into a large
   segmented search panel; on mobile a "Where to?" pill that opens a full-screen search.
4. **Category bar.** A row of icon + label tabs under the header (Airbnb's "Beachfront, Cabins,
   ..."). Ghurify's categories are the 8 `DestinationKind` values.
5. **Listing card text stack.** Under the image: bold place line, grey title, grey dates, bold
   price + "per person". No borders around cards.
6. **Listing page.** Title row with share, wide media header, then a two-column body: left is a
   stack of sections split by hairlines (host line, highlights, description, what is included,
   itinerary, where you'll meet, meet your host, things to know); right is a sticky reservation
   card with a shadow. On mobile a fixed bottom bar: price on the left, action on the right.
7. **"You won't be charged yet."** Airbnb's request-to-book reassurance fits Ghurify exactly:
   requesting to join costs nothing; payment comes only after the host approves.
8. **Confirm and pay page.** Back arrow + "Confirm and pay", left column of trip details and
   policy, right sticky summary card with a thumbnail and price details.
9. **Trips, Messages, Account, Hosting.** Airbnb's Trips page (upcoming cards + "Where you've
   been"), three-pane Messages (conversations, thread, trip details), Account card grid with
   inline "Edit" rows, and the host "Today" dashboard with reservation tabs.
10. **Host flow.** Airbnb's full-screen "become a host" flow: one big question per screen, a
    minimal header with "Save & exit", a segmented progress bar at the bottom, "Back" text link
    and a dark "Next" button.
11. **Modals and sheets.** Centred modals with an X top-left, a title centred in the header, a
    hairline under it and a sticky footer ("Clear all" link left, dark button right). On mobile
    they slide up as full-height sheets.
12. **Motion.** Small and physical: buttons shrink to 0.96 on press, cards and media scale
    slightly on hover, panels ease in at 200-300ms. Everything respects reduced motion.

### Screen-to-reference map

| Ghurify screen               | Airbnb reference                                      |
| ---------------------------- | ----------------------------------------------------- |
| Header + search              | Header with pill search, expanded search panel        |
| Home                         | Homepage: category bar + listing grid                 |
| Explore trips                | Search results: grid + "Show map" toggle + Filters modal |
| Trip detail                  | Listing page + reservation card                       |
| Destination                  | Experiences / city landing page                       |
| Sign in                      | "Log in or sign up" modal                             |
| Account, verification        | Account settings, Personal info, identity verification |
| My trips                     | Trips page                                            |
| Checkout, payment result     | Confirm and pay, booking confirmation                 |
| Group chat                   | Messages (three panes)                                |
| Public profile               | User profile (left identity card, reviews, listings)  |
| Write reviews                | Post-stay review flow (one question per screen)       |
| Host dashboard               | Hosting "Today" + Listings table                      |
| Trip wizard                  | "Become a host" step flow                             |
| Join requests                | Reservation requests                                  |
| Host payouts                 | Earnings and transaction history                      |
| Stories feed                 | No Airbnb equivalent: apply its card and type language |
| Trip safety, admin desk      | No public equivalent: Airbnb restraint, safety first   |

---

## Page inventory

29 routes, 26 distinct screens, plus the shell and 4 dialogs. All API paths are under `/api/v1`.

| #    | Route                          | Screen                       | Access                      | Main API                                                    |
| ---- | ------------------------------ | ---------------------------- | --------------------------- | ----------------------------------------------------------- |
| D1   | (every page)                   | Header, search, bell, footer | Everyone                    | `GET /me/notifications`, `GET /health`, `/hubs/notify`      |
| D2   | `/`                            | Home                         | Everyone                    | `GET /destinations`, `GET /trips?pageSize=…`                |
| D3   | `/trips`                       | Explore trips                | Everyone                    | `GET /trips` (filters, paging), `GET /destinations` (map)   |
| D4   | `/trips/:id`                   | Trip detail                  | Everyone                    | `GET /trips/{id}`, `GET /users/{hostId}`, `GET /me/bookings`|
| D5   | `/destinations/:slug`          | Destination                  | Everyone                    | `GET /destinations/{slug}`, `GET /trips?destination=`       |
| D6   | `/login`                       | Log in or sign up            | Anonymous                   | `POST /auth/otp`, `POST /auth/verify`                       |
| D7   | `/feed`                        | Stories feed                 | Everyone (post: signed in)  | `GET /feed`, posts, likes, comments, media upload           |
| D8   | `/users/:id`                   | Public profile               | Everyone                    | `GET /users/{id}`, `GET /users/{id}/posts`, follow          |
| D9   | `*`                            | Not found                    | Everyone                    | none                                                        |
| D10  | `/account`                     | Account                      | Signed in                   | `GET/PUT /me/profile`, `GET /me/verification`, become host  |
| D11  | `/account/verify`              | Identity verification        | Signed in                   | `POST /me/verification`                                     |
| D12  | `/me/trips`                    | Trips                        | Signed in                   | `GET /me/bookings`, `GET /me/refunds`, `GET /me/chats`      |
| D13  | `/bookings/:id/checkout`       | Confirm and pay              | Signed in (owner)           | `GET /bookings/{id}/checkout`, `POST …/payments`            |
| D14  | `/payments/sandbox`            | Sandbox gateway              | Signed in                   | `GET/POST /payments/sandbox/{reference}`                    |
| D15  | `/payments/result`             | Booking confirmation         | Signed in                   | `GET /bookings/{id}/checkout` (polled)                      |
| D16  | `/trips/:id/chat`              | Messages                     | Host + paid travellers      | `GET/POST /trips/{id}/chat`, `GET /me/chats`, `/hubs/chat`  |
| D17  | `/trips/:id/safety`            | Trip safety (SOS, check-ins) | Host + paid travellers      | SOS, check-ins                                              |
| D17b | `/trips/:id/review`            | Write reviews                | Trip members                | `GET /trips/{id}/reviewable`, `POST …/reviews`              |
| D18  | `/host/trips`                  | Hosting dashboard            | Host                        | `GET /me/trips`, publish, cancel                            |
| D19  | `/host/trips/new`, `/:id/edit` | Trip creation flow           | Host                        | `POST/PUT /trips`, `POST /trips/{id}/publish`               |
| D20  | `/host/trips/:id/requests`     | Join requests                | Host (owner)                | `GET /trips/{id}/join-requests`, approve, decline           |
| D21  | `/host/payouts`                | Earnings                     | Host                        | `GET /me/payouts`                                           |
| D22  | `/admin`                       | Admin overview               | Admin / SafetyDesk / Moderator | `GET /admin/dashboard`                                   |
| D23  | `/admin/verifications`         | Verification queue           | Admin                       | `GET /admin/verifications`, review                          |
| D24  | `/admin/reports`, `/disputes`  | Reports and disputes         | Admin, Moderator (disputes: Admin) | `GET /admin/reports`, resolve                        |
| D25  | `/admin/destinations`          | Destination safety status    | Admin / SafetyDesk          | `GET /destinations`, `POST /admin/destinations/{slug}/status` |
| D26  | `/admin/sos`                   | Live SOS board               | Admin / SafetyDesk          | `GET /admin/sos`, missed check-ins, `/hubs/safety`          |
| D27  | `/admin/payouts`               | Payout queue                 | Admin                       | `GET /admin/payouts`, approve                               |
| D28  | (dialogs)                      | Join, cancel, report, SOS    | Varies                      | join request, cancellation quote, reports                   |

---

## Data facts every prompt depends on

These are true of the API today. They decide where an Airbnb pattern fits and where it does not.

- **No photos for trips or destinations.** `TripSummary`, `TripDetail` and `DestinationSummary`
  have no image field. The `Scenery` illustration, keyed by `DestinationKind`
  (`Hills | Beach | Island | Forest | Wetland | TeaGarden | Lake | River`), fills every Airbnb
  photo slot. No photo carousels, no 5-photo grid. Only feed posts carry media
  (`PostView.media[]`, image or video, up to 10).
- **No avatars.** Use an initial on a coloured disc, colour derived from the user id.
- **No wishlists or saved trips.** No heart button on cards.
- **Ratings exist only on `PublicProfile`** (`asHostAverage`, `asHostCount`, `asTravelerAverage`,
  `asTravelerCount`, `reviews[]`). Trip cards cannot show stars without one request per card,
  which the rules forbid. The trip page can show them: it fetches the one host profile
  (`GET /users/{host.id}` is anonymous).
- **No search by destination type.** The category bar filters the destination list on the home
  page (client-side; all destinations are already loaded). It does not filter trip search.
- **Map pins come from destinations.** `TripSummary.destination` has no coordinates; join it by
  `slug` with `GET /destinations` (`latitude`, `longitude`, nullable).
- **Numbers may arrive as strings** (`number | string`). Always go through `asNumber()`.
- **Money** is BDT, `decimal`. Format with `formatMoney(value, language)`; Bangla uses Bangla
  digits. Show the server's `fee` and `total`; never compute them.
- **Times** are UTC ISO strings. Display in `Asia/Dhaka` via `lib/format.ts`.
- **Names are nullable** (`displayName`, `hostName`, `senderName`, `authorName` …). Every design
  needs the existing fallbacks ("a host", "someone").
- **Bilingual data**: destinations carry `name/nameBn`, `division/divisionBn`,
  `summary/summaryBn`, `statusNote/statusNoteBn`. Trip titles, summaries, itineraries and posts
  are single-language user text: never translate them.
- **Women-only visibility**: who is asking decides whether `WomenOnly` trips are returned. Never
  show a "hidden trips" count.
- **Seat hold**: approval holds a seat for **30 minutes** (`holdExpiresAt`). Service fee is
  **2%** of the trip price, sent by the server at checkout.
- **Refund rule**: 14+ days before departure: trip price back (fee kept); 7–13 days: half;
  under 7: none; host cancels or destination closed: everything back, fees included.
- **Verification levels**: `Phone`, `Nid`, `NidSelfie`. Hosting and publishing need `NidSelfie`.
  Joining a trip can return 403 when verification is missing.
- **Trips have fixed dates and one seat per request.** Airbnb's date picker and guest counter in
  the reservation card become read-only date boxes and "1 traveller".

---

## The process block (used by every prompt)

```
PROCESS
1. Explore: read CLAUDE.md, docs/DESIGN-PROMPTS.md (the reference section and data facts),
   web/ghurify-web/src/styles/index.css, components/Field.tsx, components/States.tsx, the
   feature folder for this page, its *Api.ts file and the matching types in
   src/api/schema.d.ts. Read the existing en.json and bn.json keys for the page.
2. Plan: name the Airbnb pattern you are applying, list components to create/change, i18n keys
   to add (bn + en), states covered, and anything the API cannot provide for the pattern. Flag
   those as questions; never invent fields or call endpoints that do not exist. No new npm
   packages without asking. Wait for my approval.
3. Implement: keep behaviour and data flow intact (queries, mutations, ownership checks, query
   string as source of truth). Change presentation and interaction only, unless the prompt says
   otherwise. Use the D0 tokens and components, never raw hex values. Do not copy Airbnb's
   brand: no Rausch red, no Cereal font, no Airbnb wording or illustrations.
4. Verify: npm run lint && npm run test && npm run build in web/ghurify-web. Update tests whose
   selectors changed; do not delete tests. Run the app and check the page at 375px, 768px,
   1128px and 1440px wide, in English and Bangla, signed out and signed in where relevant.
5. Summarize: what changed, which states were checked, open questions, suggested commit message.
```

---

## Prompt D0: Design system foundation (Airbnb-style)

```
Act as a senior product designer and front-end engineer. Rebuild Ghurify's visual foundation
in the style of Airbnb's design language: a quiet white canvas, near-black type, hairline
dividers, large rounded media, one strong accent button per screen. Keep Ghurify's own brand
colours and Bangla fonts. This prompt changes shared styles and components only; pages are
redesigned in later prompts.

1. Colour tokens in src/styles/index.css @theme. Keep the existing brand tokens (hill, deep,
   turmeric, jamdani, mist, river, sky, sand, dusk) and add semantic tokens on top:
   - canvas #ffffff (page background; body moves from bg-sand to bg-canvas)
   - ink #222222 (primary text), ink-muted #6a6a6a (secondary text, 5.3:1 on white),
     ink-subtle #929292 (only for large text and disabled)
   - hairline #dddddd (dividers, card borders), field-border #b0b0b0, surface-muted #f7f7f7
   - brand = jamdani. The main call to action uses a jamdani gradient, Ghurify's answer to
     Airbnb's gradient button: linear-gradient(to right, #8c2650, #a3305c, #b8486f), hover
     shifts the gradient to the right. Only one brand button per screen.
   - dark button #222222 (wizard "Next", modal "Show 48 trips", secondary emphasis)
   - trust = hill (verified badges, success, Ghurify logo); highlight = turmeric (few seats
     left, new requests); keep sand only for promotional bands.
   - danger #c13515 (errors, destructive actions; separate from jamdani so a brand button
     never reads as an error), warning amber-800 on amber-50, success hill on emerald-50,
     info river on sky. Destination status open / caution / closed reuse StatusBadge colours.
   Verify every text/background pair passes WCAG AA.
2. Typography: Hind Siliguri for all UI text, weights 400 / 500 / 600 / 700, as Airbnb uses one
   family throughout. Baloo Da 2 is kept only for the logo and the home hero headline.
   Scale (px / line height, Bangla needs the extra leading):
   hero 48/1.15 (mobile 32), h1 32/1.25 (mobile 26), h2 22/1.35, h3 18/1.4, body 16/1.6,
   small 14/1.55, caption 12/1.5. Titles 600, body 400. Never uppercase or letter-space Bangla.
3. Layout: Airbnb grid breakpoints and gutters. Page gutter 24px (< 744px), 40px (744-1127),
   80px (>= 1128). Content max widths: grids 1760px, listing/detail pages 1120px, forms and
   settings 680px. Card grid columns: 1 (< 550), 2 (550-949), 3 (950-1127), 4 (1128-1439),
   5 (1440-1879), 6 (>= 1880). Grid gap 24px column, 40px row.
4. Shape and depth: radius 8 (buttons, inputs), 12 (cards, media, modals), 16 (bottom sheets,
   top corners), full (pills, avatars). Elevations:
   e1 search pill  0 1px 2px rgba(0,0,0,.08), 0 4px 12px rgba(0,0,0,.05)
   e2 sticky card  0 6px 16px rgba(0,0,0,.12) with a 1px hairline border
   e3 modal/menu   0 8px 28px rgba(0,0,0,.28)
   Sections inside a page are separated by a 1px hairline with 32px (mobile 24px) padding,
   not by boxed cards.
5. Components in src/components (extend Field.tsx and States.tsx, add files where needed):
   - Button: brand (gradient), dark, outline (1px #222 border, white fill), ghost, text link
     (underlined, 600), danger; sizes sm 36 / md 48 / lg 56 height; press scales to 0.96;
     loading shows three animated dots in place of the label (Airbnb style); min touch target
     44px. Replace primaryButtonClass and friends, keeping the old exports until pages move.
   - Icon button: 32px circle, white with hairline (share, close, back).
   - Pill search bar and search panel (built in D1, primitives here): segment with a small
     600 label and a muted value; active segment white with e3 shadow on a grey panel.
   - Category tab: 24px line icon above a 12px label, 600 underline when active, muted when
     not; horizontally scrollable row with fade edges and arrow buttons on desktop.
   - Media tile: rounded 12px frame holding Scenery, aspect 20:19 for cards, optional top-left
     badge chip (white pill, 600, 12px) and no heart.
   - Listing text stack: line 1 bold, lines 2-3 muted, price line with bold amount.
   - Badge / chip: neutral, trust, highlight, danger, info; restyle GroupBadge, StatusBadge,
     SeatsBadge, VerificationBadge on it. Women-only is a solid jamdani chip with white text.
   - Fields in Airbnb's stacked style: 56px boxes with the label inside at the top (small,
     muted) and the value below; border field-border; focus = 2px ink border; grouped fields
     share borders (rounded only on the outer corners); error = danger border + message below
     with an icon; counters for any maxLength field, in Bangla digits when bn.
   - Settings row: label, current value muted, "Edit" text link on the right that expands the
     row into its form with Save / Cancel (Airbnb Personal info pattern).
   - Avatar: initial on a disc, colour picked from the user id from 6 fixed colours; sizes
     24 / 40 / 64 / 104. Optional small verified shield overlay.
   - Rating: star icon + "4.8 · 12 reviews"; only rendered when a value exists.
   - Modal: header 64px with X on the left, centred title, hairline; scrollable body; sticky
     footer with hairline (left text link, right dark or brand button). Mobile: full-height
     sheet sliding up, 16px top radius. Focus trap, Esc closes, focus returns. Add a
     ConfirmDialog on it to replace every window.confirm (HostTripsPage uses one).
   - Bottom action bar (mobile): fixed, white, hairline top, safe-area padding; left price
     or info, right button.
   - Toast: dark #222 pill bottom-left on desktop, bottom-centre on mobile.
   - Skeletons matching each card and list; EmptyState (Scenery, title, hint, outline button);
     ErrorState with retry; inline Alert (info, warning, danger, success).
   - Tabs: text tabs with a 2px ink underline (Trips, Hosting); segmented control for admin.
   - Countdown (restyle HoldCountdown): mm:ss with Bangla digits, danger under 5 minutes.
6. Icons: one inline-SVG line icon set in src/components/icons (24px grid, 2px stroke,
   rounded caps, currentColor), drawn in Airbnb's simple outline style. Include the 8
   destination kinds, search, globe, menu, user, bell, share, close, back, chevrons, map,
   list, filter, shield, lock, calendar, users, star, chat, flag, phone, sos, check. Replace
   every emoji used as a UI icon. Do not add an icon package without asking.
7. Motion: 150ms press/hover, 250ms panels, 300ms sheets, cubic-bezier(0.2, 0, 0, 1); media
   tiles scale 1.03 on card hover; reduced motion disables all of it (already in index.css).
8. Add a dev-only route /dev/ui (only when import.meta.env.DEV) rendering every component in
   every state, in both languages, to check later prompts against.

i18n: any new UI text gets bn and en keys. Sentence case everywhere.
```

---

## Prompt D1: App shell (header, pill search, category bar, menus, footer)

```
Redesign the shell: AppShell in src/app/router.tsx, components/SiteHeader.tsx,
components/NotificationBell.tsx, components/SiteFooter.tsx and components/tour/*.
Reference: Airbnb's header with the pill search and the user menu.

Header (desktop, 80px, white, hairline bottom once scrolled):
- Left: Ghurify logo (hill, Baloo Da 2 wordmark) -> "/".
- Centre: the compact search pill "Anywhere · Any dates · Add travellers" with a round brand
  search button (40px). Clicking any segment expands the header into the large search panel
  (see below). On the home page the header starts expanded and collapses into the pill on
  scroll, like Airbnb.
- Right: "Lead a trip" text link (-> /login signed out, /account to become a host, or
  /host/trips for hosts; label "Switch to hosting" for hosts), globe icon button opening the
  language modal (English / বাংলা as two large option cards), notification bell (signed
  in), then the user menu pill: hamburger icon + Avatar (or a generic user icon when signed
  out), 42px, hairline border, e1 shadow on hover.
- While auth status is "unknown" (silent refresh) keep the avatar slot as a neutral circle so
  nothing jumps.

Search panel (expanded state, max width 850px, grey #ebebeb container with white active
segment), mapping only to real API params:
- "Where": destination; dropdown lists destinations in a 3-column grid of small Scenery tiles
  with names (bn when bn) and "Any destination" first. Sets `destination`.
- "From" and "To": a two-month calendar (Asia/Dhaka, min today, Bangla month names and digits
  when bn); sets `from` and `to`.
- "Who": group type options as rows with a description (Open, Women only, Students, Families)
  plus a "Travellers" stepper 1-10 that sets `minSeats`.
- Search button expands to a brand pill "Search" when the panel is open. Submit -> /trips?…
- Mobile: the header becomes a single full-width "Where to?" pill (search icon, two lines:
  "Where to?" and "Any destination · Any dates") with a round filter icon button on the
  right. Tapping opens a full-screen search sheet with the same three steps as stacked cards
  (only the active one expanded), "Clear all" link and brand "Search" button in the footer.

User menu (dropdown 240px, e3, radius 12):
- Signed out: "Log in" (600), "Sign up", hairline, "Lead a trip", "Help with safety"
  (-> /#safety).
- Signed in: "Trips" (/me/trips), "Messages" (the latest chat from GET /me/chats, total unread
  badge summed from `unread`), "Notifications", "Account" (/account), hairline, "Hosting"
  (/host/trips) and "Earnings" (/host/payouts) for Host role, "Admin" for isStaff roles,
  hairline, "Log out". Header data from GET /me/profile (displayName, maskedEmail,
  verifiedLevel).

Mobile bottom tab bar (< 744px, Airbnb style: icon above a 10px label, brand colour when
active, hides on scroll down and returns on scroll up): Explore (/trips), Stories (/feed),
Trips (/me/trips, signed in) or Log in, Messages (latest chat, unread dot) when signed in,
Profile (/account). Hosts get Hosting instead of Stories. Hide the bar on chat, checkout,
the trip creation flow, payment pages and the safety page.

Notification bell (GET /me/notifications -> { items[], unreadCount }):
- Icon button with a jamdani count dot (99+ cap, Bangla digits when bn).
- Panel: desktop dropdown 400px, mobile full-screen sheet. Header "Notifications" + "Mark all
  as read" (POST /me/notifications/read { upToId: newest id }). Rows like Airbnb's inbox: icon
  disc per `kind`, the sentence from notifications.kinds.<kind> with JSON.parse(item.data),
  relative time, unread dot. Grouped Today / Earlier (Asia/Dhaka). Keep linkFor() routing.
  safety.sos and destination.closed use the danger icon disc; join_request.approved uses the
  brand disc (it starts the 30-minute payment clock). Live via /hubs/notify.

Footer (Airbnb style, #f7f7f7, hairline top): three columns of links in 14px: Explore (Trips,
Destinations, Stories), Safety (How SOS works -> /#safety, Women-only trips ->
/trips?groupType=WomenOnly, Emergency 999 -> tel:999), Hosting (Lead a trip, Hosting
dashboard). Bottom row: © year · language button · the live SystemStatus as a small grey
"All systems normal" text with a green dot (GET /health; red dot + retry when down).

Guided tour: restyle as Airbnb-style tooltips (white, radius 12, e3, small arrow, step count
"2 of 6", dark "Next" button); keep all data-tour anchors; never show it on mobile.

Accessibility: skip link, landmarks, aria-current, full keyboard support for the search panel,
menus and calendar (arrow keys, Esc, focus return).
```

---

## Prompt D2: Home page (`/`)

```
Redesign src/app/HomePage.tsx. Reference: Airbnb's homepage, where the search and the
category bar sit at the top and the content is a grid of listings. Keep a short Ghurify
story below the grids, because first-time visitors need to learn what a "group trip" is and
why it is safe.

Data:
- GET /destinations -> DestinationSummary[]: slug, name/nameBn, division/divisionBn,
  summary/summaryBn, kind, status (Open|Caution|Closed), statusNote/statusNoteBn, latitude,
  longitude, upcomingTrips, fromPrice (nullable).
- GET /trips?pageSize=12 -> TripPage { items: TripSummary[], totalCount, page, pageSize }.
  TripSummary: id, title, destination { slug, name, nameBn, kind, status }, startDate, endDate,
  seats, seatsLeft, pricePerPerson, groupType, status, hostName, hostVerifiedLevel.
- GET /trips?groupType=WomenOnly&pageSize=4 for the women-only row.

Sections:
1. Header with the expanded search panel (D1); a one-line headline above it in Baloo Da 2
   ("Travel Bangladesh together, safely") and nothing else: no big hero image.
2. Category bar (sticky under the header on scroll): "All" + the 8 DestinationKind values
   with their icons (Hills, Beach, Island, Forest, Wetland, Tea garden, Lake, River). It
   filters the destination grid below on the client and updates ?kind= in the URL. It does not
   filter trip search (the API has no kind filter).
3. Destinations grid (Airbnb card grid): media tile = Scenery by kind (20:19, radius 12),
   top-left chip = status when not Open ("Caution" amber, "Closed" danger); text stack:
   **Name** (bn when bn) | division muted | "N upcoming trips" muted | **from ৳X** (only when
   fromPrice is not null). Closed destinations stay visible with a 60% desaturated tile.
4. "Leaving soon" row: section title + "Show all (48)" link (totalCount) + left/right circle
   arrows on desktop; a horizontal scroll of TripCards (D3) with snap on mobile.
5. "Women-only trips" row in the same format, only when it returns items (a safety feature,
   it deserves its own row).
6. "How Ghurify works": 4 steps in a row with large line icons (find, request for free, pay
   into escrow when approved, travel with SOS), plain text, no cards.
7. Safety band: full-width section on #f7f7f7 with the five safety promises in a 5-column row
   (verified hosts, escrow, SOS on every trip, women-only trips, closure alerts). No
   invented statistics.
8. Hosting band (Airbnb's "Become a host" block): split layout, left a large rounded Scenery
   (Hills) tile, right "Lead a trip in Bangladesh" + one sentence + dark button -> /login,
   /account or /host/trips/new by role.

States: skeleton grid tiles (grey rounded rectangle + 3 grey text lines) sized like the real
cards; ErrorState with retry per section, so one failing list never blanks the page; empty
row/section messages. No testimonials, user counts, photos or ratings: the API has none.
```

---

## Prompt D3: Explore trips (`/trips`) and the trip card

```
Redesign src/features/trips/ExplorePage.tsx and TripCard.tsx. Reference: Airbnb search
results: a filter row, a responsive card grid, a floating "Show map" pill, and a split
list/map view on wide screens. The URL query string stays the single source of truth.

Data: GET /trips with Destination, From, To, MaxPrice, GroupType, MinSeats, Sort (Soonest |
PriceLowToHigh | PriceHighToLow), Page, PageSize (24 for the grid), VerifiedHostsOnly ->
TripPage { items, totalCount, page, pageSize }. GET /destinations for names and map pins
(join TripSummary.destination.slug to latitude/longitude).

Layout:
- Header in its compact pill state, showing the current search ("Sajek · 12–14 Oct · 2
  travellers").
- Filter row under the header (sticky, hairline bottom): quick filter chips (Women only,
  Students, Families, Verified hosts, Under ৳7,500) that toggle the matching query param,
  then a "Filters" outline button with a count badge on the right, and the sort as a small
  dropdown.
- Result line: "48 trips" (600) + "in Sajek Valley" when a destination is set.
- Desktop >= 1128px: split view, card grid on the left (2-3 columns), map on the right (sticky,
  full height under the header, radius 12). Toggle between "Show map" and "Show list" with a
  floating dark pill button (bottom centre, map/list icon), exactly like Airbnb. Below 1128px
  the grid is full width and the pill switches to a full-screen map.
- Map (Leaflet, already a dependency; load it lazily as DestinationPage does): one white price
  pill marker per destination in the results ("৳6,500", the lowest pricePerPerson among
  results there; turns dark when hovered or selected); hovering a card highlights its marker;
  clicking a marker shows a small card popup with the trips at that destination. Trips whose
  destination has no coordinates still appear in the list.
- Filters modal (D0 modal, 780px): sections split by hairlines: Price (max price: preset chips
  ৳5,000 / ৳7,500 / ৳10,000 / ৳15,000 + a number field), Dates (from/to), Group type (four
  large selectable cards with icons and one-line descriptions), Travellers (minSeats stepper
  1-10), Host (verified hosts only switch). Footer: "Clear all" link, dark "Show 48 trips"
  button (re-count with a debounced query while the modal is open).
- Pagination: Airbnb style numbered circles (1 2 3 … 8) with prev/next chevrons, centred,
  plus "Showing 1 – 24 of 48 trips"; scroll to top on page change.

TripCard (used everywhere):
- Media tile: Scenery by destination.kind, 20:19, radius 12, scales 1.03 on hover.
  Top-left chip, one only, in priority: "Full" (neutral) > "Women only" (jamdani) >
  "Only N seats left" (when seatsLeft <= 3, turmeric) > destination "Caution" (amber).
- Text stack (no card border, 12px under the tile):
  line 1: **{destination name}** (bn when bn) and, right-aligned, a small verified shield
          when hostVerifiedLevel is set
  line 2: {title}, muted, one line with ellipsis
  line 3: {date range} · {N days}, muted
  line 4: **৳8,500** per person
- Whole card is one link to /trips/:id (keep the stretched-link pattern). No heart, no rating.

States: skeleton grid; while refetching keep the old cards and show a thin top progress bar;
empty state "No exact matches" + "Try changing or removing some of your filters" + "Remove
all filters" outline button (Airbnb wording style, our own text); error with retry. Announce
the result count to screen readers.
```

---

## Prompt D4: Trip detail (`/trips/:id`)

```
Redesign src/features/trips/TripDetailPage.tsx as an Airbnb listing page. It is the most
important conversion page.

Data:
- GET /trips/{id} -> TripDetail: id, title, summary, destination { slug, name, nameBn, kind,
  status, statusNote, statusNoteBn }, startDate, endDate, meetingPoint, seats, seatsLeft,
  pricePerPerson, groupType, status (Draft|Published|Full|Cancelled|Completed), host { id,
  displayName, memberSince, verifiedLevel }, costItems[] { category (Transport|Stay|Food|Fees|
  Guide|Buffer), description, amount }, itinerary[] { dayNo, title, details, difficulty
  (Easy|Moderate|Challenging) }, groupMix { women, men, others } (nullable).
- GET /users/{host.id} (anonymous) -> PublicProfile: asHostAverage, asHostCount, reviews[]
  (filter direction == TravelerToHost), hostedTrips, bio, homeDistrict, isHost.
- GET /destinations/{slug} only when you need latitude/longitude for the map.
- Signed in: GET /me/bookings (cached) to find the row with tripId == this trip.

Layout (max width 1120px):
1. Title row: h1 title (26-32px, 600); below it a muted line "★ 4.8 · 12 reviews of the host ·
   Sajek Valley, Rangamati" (rating part only when asHostAverage exists; the place links to
   /destinations/:slug); right side "Share" and "Report" text buttons with icons.
2. Media header: one wide Scenery tile (radius 12, 2.4:1 desktop, 4:3 mobile edge-to-edge)
   with the GroupBadge chip top-left. No photo grid (there are no photos).
3. Two columns: left 58% content, right 33% sticky reservation card (top 112px).
   Left column sections, separated by hairlines:
   a. Host line: "Group trip in Sajek Valley hosted by Rizvi" (h2) + muted line "10 seats ·
      3 days · Moderate" (hardest difficulty) + Avatar on the right.
   b. Destination alert when status != Open: amber (Caution) or danger (Closed) Alert with
      the statusNote in the UI language.
   c. Highlights (Airbnb's three icon rows, 600 title + muted line): "Verified host"
      (VerificationBadge level), "Escrow-protected payment", and the group type
      ("Women-only group" / "Students" / "Families" / "Open to everyone").
   d. Description: summary, clamped to 6 lines with "Show more >" opening a modal.
   e. "What your money covers" (Airbnb's "What this place offers"): 2-column list of
      cost items, each with its category icon, category name and description, amount on the
      right; a stacked bar showing the split; total = pricePerPerson. "Show all N items"
      opens a modal when there are more than 6.
   f. Itinerary: "Day 1 · title" rows with a vertical line, difficulty chip, details; first 3
      days shown, "Show all N days" button (outline) for the rest.
   g. "Where you'll meet": meetingPoint text + a Leaflet map (radius 12, 480px tall desktop)
      centred on the destination coordinates when they exist.
   h. Reviews (only when the host has TravelerToHost reviews): big "★ 4.8 · 12 reviews" header,
      2-column grid of the 6 newest: Avatar, reviewer name, trip title, month in Asia/Dhaka,
      body clamped to 3 lines; "Show all 12 reviews" opens a modal. Label clearly that these
      are reviews of the host across their trips.
   i. "Meet your host" (Airbnb host card): left a white card with e2 shadow: large Avatar,
      name, "Host" label, stats stacked on the right (reviews count, rating, years on Ghurify
      from memberSince); right: bio, home district, verification line, "View profile" dark
      button -> /users/{host.id}. Safety note: "To protect your payment, never pay outside
      Ghurify."
   j. "Things to know" (3 columns): Group (group type, seats, group mix "3 women · 2 men" when
      groupMix exists and seats are taken), Safety (SOS, check-ins, verified host),
      Cancellation (the 5 refund rules as short lines; "Show more" modal with the full table).
4. Reservation card (white, hairline, radius 12, e2, 24px padding):
   - "৳8,500 / person" (price 22px 600).
   - Bordered box (radius 8) split in two: "Starts" | "Ends" dates (read-only), and below it
     "Travellers: 1 seat" and "Seats left: 4 of 10" with a small seat-dot row.
   - The action button full width (state matrix below).
   - "You won't be charged yet" centred muted line under a request button.
   - Price lines: "৳8,500 trip price", "Service fee (2%) shown at checkout", hairline,
     "Total before fee ৳8,500".
   - Under the card: "Report this trip" flag link (signed in, not own trip) -> ReportDialog.
   Mobile: the card is replaced by the fixed bottom action bar: left "৳8,500 / person" bold and
   the date range underlined (opens the dates modal), right the action button.

Action button state matrix (one button, same place, brand gradient unless noted):
- Signed out -> "Log in to join" -> /login (return here after sign-in).
- Own trip (user id == host.id) -> dark "Manage requests" -> /host/trips/:id/requests.
- My booking row: Pending -> outline disabled "Request sent" + "Withdraw request" text link;
  Held -> "Pay ৳{amount}" -> /bookings/{bookingId}/checkout with the Countdown above it;
  Confirmed -> success panel "You're going!" + dark "Open messages" + "Safety" link;
  Declined/Expired/Cancelled -> allow a new request if seats are left.
- Full / seatsLeft 0 -> disabled "Fully booked". Cancelled / Completed -> disabled with status.
- Destination Closed -> disabled, with the reason.
- Otherwise -> "Request to join" -> JoinRequestDialog (D28).

States: 404 not-found page; loading skeleton matching the layout (title bar, media block, two
columns); error with retry; host profile failing must not break the page (just hide reviews).
Share uses navigator.share with a copy-link + toast fallback.
```

---

## Prompt D5: Destination (`/destinations/:slug`)

```
Redesign src/features/trips/DestinationPage.tsx (keep it lazy-loaded with Leaflet).
Reference: an Airbnb city/experiences landing page: a big visual, a short intro, then
listings and a map.

Data: GET /destinations/{slug} -> DestinationSummary (fields in D2); trips from
GET /trips?destination={slug}&pageSize=24.

Layout (max width 1120px):
- Media header: large rounded Scenery tile (2.4:1) with the name (bn when bn) overlaid bottom
  left in white 48px 600 over a soft gradient, "{kind} · {division} division" above it.
- Safety status row directly under it, full width, always visible: Open = hill check icon
  "Open for travel"; Caution = amber Alert with statusNote; Closed = danger Alert with
  statusNote and "Travellers booked on trips here are refunded in full." The most important
  fact on the page.
- Intro: summary in the UI language (h2 "About {name}"), and a stats line "12 upcoming trips
  · from ৳6,500" (fromPrice only when not null).
- "Trips to {name}" grid of TripCards (3 columns desktop) + "Show all with filters" text link
  -> /trips?destination={slug}.
- "Where it is" section: Leaflet map (radius 12, 400px, scroll-zoom off) with a brand marker,
  only when latitude and longitude exist, plus an "Open in maps" link (mapLink()).
- No "stories from here": the feed has no destination filter.

States: 404, loading skeleton, error, no trips (empty state + "Lead a trip here" for hosts ->
/host/trips/new).
```

---

## Prompt D6: Log in or sign up (`/login`)

```
Redesign src/features/auth/LoginPage.tsx in the style of Airbnb's "Log in or sign up" modal:
a centred 568px card (radius 12, e3) on a light grey page, with an X / back control in the
header, title centred, hairline under the header. Mobile: full screen.

Data: POST /auth/otp { email } -> { expiresInSeconds, resendAfterSeconds };
POST /auth/verify { email, code } -> SessionResponse { accessToken, expiresInSeconds,
user { id, maskedEmail, displayName } }. Errors: 429 too many requests, 401 wrong/expired code,
403 account unavailable.

Step 1 - header "Log in or sign up":
- h2 "Welcome to Ghurify" (22px 600).
- One stacked field "Email" (56px, label inside), helper "We'll email you a 6-digit code.
  No password needed." Lower-case and trim before sending.
- Brand "Continue" button, full width, 48px.
- Divider "or" is NOT needed: there is no social sign-in. Under the button, three small
  trust lines with icons (verified hosts, escrow payments, SOS on every trip).
Step 2 - header "Confirm your email" with a back arrow:
- "Enter the code we sent to {email}" + "Change" link.
- 6 separate digit boxes (56px squares, radius 8), with a hidden autocomplete="one-time-code"
  input so autofill and paste fill all six; auto-submit on the 6th digit.
- Muted "Code expires in 10 minutes" (from expiresInSeconds), "Didn't get it? Check spam or
  resend" with the resendAfterSeconds countdown.
- Errors inline under the boxes, specific per status code.
After success return to the previous page (location state or ?returnTo), default "/".
A new user (displayName null) goes to /account with a "Finish your profile" prompt.
Accessibility: each box labelled "Digit 1 of 6", errors announced, sensible focus moves.
```

---

## Prompt D7: Stories feed (`/feed`)

```
Redesign src/features/feed/FeedPage.tsx, PostCard.tsx and StoryComposer.tsx. Airbnb has no
feed, so apply its visual language to a social feed: white canvas, hairline dividers between
posts instead of cards, rounded 12px media, 600 names, muted metadata.

Data:
- GET /feed?before={cursor} -> PostPage { items: PostView[], nextBefore }. PostView: id,
  authorId, authorName, authorVerifiedLevel, body, destinationSlug, destinationName,
  destinationNameBn, tripId, likes, comments, likedByMe, created, media[] { id, kind
  (Image|Video), contentType, url }.
- Like/unlike: POST/DELETE /posts/{id}/likes. Comments: GET/POST /posts/{id}/comments ->
  CommentView { id, postId, authorId, authorName, body, created }. Delete own post:
  DELETE /posts/{id}.
- Compose: POST /media/upload-url { contentType, sizeBytes } -> { mediaId, uploadUrl,
  expiresOn }; PUT to uploadUrl (keep uploadToStorage with progress); POST /media/{id}/complete;
  POST /posts { body, destinationSlug, tripId, mediaIds }. Up to 10 files: jpeg, png, webp,
  mp4, quicktime. Body max 2000.

Layout: centred 600px column; on >= 1128px a right rail (sticky): "Popular destinations"
(GET /destinations sorted by upcomingTrips, small Scenery thumbs + name + trip count) and a
"Lead a trip" card.
- Composer: a "Share a story from your trip" pill with your Avatar; opens a modal (D0) with
  textarea + counter, destination select ("Add a place"), media picker showing a thumbnail
  grid with per-file progress rings and remove buttons, privacy note, footer "Post" brand
  button (disabled until there is text or media and uploads finished). Signed out: an inline
  "Log in to share your trip stories" row.
- Post: Avatar + **name** (-> /users/{authorId}) + verified shield, muted "· 2h" and the place
  chip (-> /destinations/{slug}, bn when bn); body clamped at 6 lines with "Show more"; media
  in a rounded 12px block (1: 4:3; 2: side by side; 3+: 2x2 with "+N"), tap opens a full-screen
  lightbox with swipe, counter "2 / 5", close X top-left; videos with controls, no autoplay
  with sound; action row of icon buttons: like (heart fills jamdani, optimistic, count),
  comment (count; expands the thread inline with an input max 1000), share, and a "…" menu
  (Delete for my posts, Report otherwise -> ReportDialog kind Post).
- Infinite scroll on nextBefore with a "Show more stories" outline button fallback.
- Alt text on every image: "Photo shared by {name}".
States: skeleton posts, empty feed ("No stories yet" + "Share the first one"), error with
retry, per-file upload failure with retry.
```

---

## Prompt D8: Public profile (`/users/:id`)

```
Redesign src/features/feed/PublicProfilePage.tsx in the style of an Airbnb user profile:
a left identity card and a right column with "About", reviews and listings.

Data: GET /users/{id} -> PublicProfile: userId, displayName, bio, homeDistrict, memberSince,
verifiedLevel, isHost, followers, following, followedByMe, asHostCount, asHostAverage
(nullable), asTravelerCount, asTravelerAverage (nullable), hostedTrips[] { id, title,
destinationSlug, destinationName, destinationNameBn, startDate, endDate, status }, reviews[]
{ id, tripId, tripTitle, reviewerId, reviewerName, direction, rating, body, created }.
GET /users/{id}/posts -> PostPage. Follow: POST/DELETE /users/{id}/follow.

Layout (max width 1120px, two columns: 340px sticky left, rest right; stacked on mobile):
- Left identity card (white, e2, radius 24, Airbnb's profile card): large Avatar (104px) with
  a verified shield, name (32px 600), "Host" or "Traveller" label; a stats column on the right
  of the card separated by hairlines: reviews (asHostCount + asTravelerCount), rating
  (asHostAverage when isHost, else asTravelerAverage; hidden when null), years on Ghurify
  (from memberSince).
- Under the card: "{name}'s confirmed information" with check rows for the verification level
  (Phone / ID / ID + selfie). Then Follow / Following button (hidden on own profile, log-in
  prompt when signed out) with "120 followers · 80 following", and a "Report this profile"
  flag link -> ReportDialog kind User.
- Right column, hairline-separated:
  "About {name}": home district row with an icon, bio.
  "{name}'s reviews": horizontal carousel of review cards (radius 12, hairline): body
  clamped to 4 lines, reviewer Avatar + name + month, trip title link, direction label
  ("as host" / "as traveller"); arrows on desktop; "Show all N reviews" modal.
  "{name}'s trips" (hosts): carousel of compact TripCard-style tiles from hostedTrips, with
  status chip and dates. hostedTrips has no destination kind, so look it up from
  GET /destinations by destinationSlug (already cached) to pick the Scenery.
  "Stories": PostCard list or a 3-column grid of post thumbnails.
- Each section has its own empty line ("No reviews yet").
States: 404 "We couldn't find this person", loading skeleton, error.
```

---

## Prompt D9: Not found (`*`)

```
Redesign src/app/NotFoundPage.tsx in Airbnb's 404 style: on the left a large "Oops!"
(Baloo Da 2), "We can't find the page you're looking for", a muted "Error code: 404", and a
short list of helpful text links ("Home", "Explore trips", "Stories", "Safety"); on the right
a rounded River Scenery illustration with a small lost boat. Stacks on mobile. No API.
```

---

## Prompt D10: Account (`/account`)

```
Redesign src/features/auth/AccountPage.tsx in the style of Airbnb's Account settings: an
"Account" landing with a grid of setting cards, each opening its section, and Airbnb's
"Personal info" rows with inline "Edit".

Data:
- GET /me/profile -> ProfileDetails: userId, maskedEmail, displayName, gender (Unspecified|
  Female|Male|Other), phone, bio, homeDistrict, emergencyContactName, emergencyContactPhone,
  roles[], verifiedLevel, memberSince.
- PUT /me/profile with UpdateProfileCommand (displayName required; the others nullable).
- GET /me/verification -> VerificationRecord[] { id, level, status, reason, created,
  reviewedOn }.
- POST /me/roles/host.

Layout (max width 1080px):
- Landing: h1 "Account", line "{displayName}, {maskedEmail} · Go to profile" (-> /users/{userId}).
  A 3-column grid of setting cards (radius 12, e2 shadow, icon top, 600 title, muted one-line
  description): Personal info, Safety contact, Identity verification, Hosting, Trips (-> /me/
  trips), Earnings (hosts, -> /host/payouts). Use ?section= in the URL to open one; on desktop
  the opened section shows with a breadcrumb "Account > Personal info"; on mobile it is a
  full page with a back arrow. A completion banner on top when something is missing
  ("Finish your profile to join trips: add your phone and emergency contact").
- Personal info: Airbnb rows (label, value or "Not provided", "Edit"/"Add" text link; editing
  one row disables the others): Legal display name, Email (read-only, masked, "This is how you
  log in"), Gender (locked once verified: verifiedLevel != null and gender != null, show a lock
  and the existing hint instead of Edit), Phone (+8801XXXXXXXXX, isBangladeshiMobile; "Used
  for SOS and payouts, never shown to other travellers"), Home district, About (bio, counter).
  Each row saves on its own with PUT (send the full command from current values), toast on
  success, field errors from the server mapped to the row.
- Safety contact: emergency contact name and phone rows (phone required when a name is
  given), with an info panel on the right explaining it is texted automatically on SOS.
- Identity verification: a 3-step vertical tracker (Phone, ID, ID + selfie) with the current
  level checked; latest record status as an Alert (Pending warning, Rejected danger with
  reason, Approved success); brand "Verify" / dark "Upgrade" -> /account/verify. Side panel:
  why we verify and what is stored.
- Hosting: not a host -> pitch with a Scenery tile and "Become a host" (disabled with an
  explanation unless verifiedLevel is NidSelfie); host -> links to Hosting and Earnings.
- "Log out" text link at the bottom of the landing.
States: loading skeleton, error with retry.
```

---

## Prompt D11: Identity verification (`/account/verify`)

```
Redesign src/features/auth/VerificationPage.tsx in the style of Airbnb's identity
verification flow: a minimal full-screen flow with one task per screen, a segmented
progress bar at the bottom, "Back" text link and a dark "Next"/"Submit" button.

Data: POST /me/verification { level (Nid|NidSelfie), nidNumber, dateOfBirth } ->
VerificationRecord { id, level, status (Pending|Approved|Rejected), reason, created,
reviewedOn }. GET /me/profile to preselect level (NidSelfie for hosts) and check displayName
and gender.

Screens:
1. "Let's verify your identity": two large selectable cards (radius 12, 2px ink border when
   selected): "National ID" (to join trips) and "National ID + selfie" (needed to host).
   If displayName or gender is missing, a warning Alert linking to /account blocks Next.
2. "Enter your national ID details": stacked fields NID number (numeric keypad; 10, 13 or 17
   digits, spaces and dashes allowed; live "13 digits" under it) and date of birth (max today,
   Asia/Dhaka). A lock panel: "Your number is sent once. We keep only a keyed hash and never
   show it again."
3. Result: Approved (hill check illustration + the new badge + "Done"), Pending ("We'll let
   you know, usually within minutes"), Rejected (reason + "Try again").
Header: Ghurify logo left, "Exit" text link right (-> /account). Clear the number after
submit (keep current behaviour). Errors inline.
```

---

## Prompt D12: Trips (`/me/trips`)

```
Redesign src/features/bookings/MyTripsPage.tsx as Airbnb's Trips page: upcoming trips as
large horizontal cards with the next step, and past trips as a grid.

Data:
- GET /me/bookings -> MyTripBooking[]: requestId, requestStatus (Pending|Approved|Declined|
  Expired|Cancelled), requestedOn, tripId, title, destinationSlug, destinationName,
  destinationNameBn, destinationKind, destinationStatus, startDate, endDate, tripStatus,
  hostName, bookingId, bookingStatus (Held|Confirmed|Cancelled|Refunded), amount,
  holdExpiresAt.
- GET /me/refunds -> RefundView[]: id, bookingId, tripTitle, amount, shortfall, reason,
  status (Pending|Succeeded|Failed), created, completedOn.
- GET /me/chats -> ChatUnread[] { tripId, title, unread }.
- Withdraw: POST /join-requests/{requestId}/cancel. Cancel a paid booking: CancelBookingDialog.

Layout (max width 1120px):
- h1 "Trips".
- Action banner when any booking is Held: brand-bordered card "Pay for {title} within 12:40
  to keep your seat" + brand "Pay now".
- "Upcoming" (Pending, Held, Confirmed with tripStatus not Completed): one wide card each
  (radius 12, e2): left half text, right half a Scenery tile by destinationKind (stacked on
  mobile, tile on top). Text side: status chip, **title** (-> /trips/{tripId}), "{destination}
  · {date range}", "Hosted by {hostName}", a 4-step progress line (Requested · Approved · Paid
  · Travel), then actions by state:
  Pending: "Waiting for the host to reply" + "Withdraw request" text link.
  Held: Countdown + brand "Pay ৳{amount}" -> /bookings/{bookingId}/checkout + "Withdraw".
  Confirmed: dark "Message the group" (unread badge from /me/chats), outline "Safety",
        "…" menu with "Cancel booking" (dialog with refund quote) and "Report a problem"
        (Dispute).
  Destination Caution/Closed: an Alert strip inside the card.
- "Where you've been" (Completed): Airbnb's grid of small rows: square Scenery thumbnail (64px,
  radius 8), title, destination, month range; a "Write a review" brand pill when
  bookingStatus is Confirmed.
- "Cancelled and declined" collapsed section (Declined, Expired, Cancelled, Refunded) in the
  same small-row style with a status chip.
- "Refunds" section (only if any): rows with trip title, reason, amount, status chip,
  shortfall explained when > 0, created / completed dates.
Empty state (Airbnb style): "No trips booked... yet!" + "Time to dust off your bags and start
planning your next adventure" + dark "Start searching" -> /trips, with a Scenery tile.
Loading skeletons, error with retry.
```

---

## Prompt D13: Confirm and pay (`/bookings/:id/checkout`)

```
Redesign src/features/payments/CheckoutPage.tsx as Airbnb's "Confirm and pay" page.

Data: GET /bookings/{id}/checkout -> BookingCheckout: bookingId, tripId, tripTitle, startDate,
endDate, hostName, amount, fee, total, status (Held|Confirmed|Cancelled|Refunded),
holdExpiresAt, latestPaymentStatus (Created|Pending|Succeeded|Failed|Expired|null),
latestPaymentFailure. Pay: POST /bookings/{id}/payments with Idempotency-Key ->
PaymentStarted { paymentId, redirectUrl, amount, fee, total }; then go to redirectUrl.
Keep the one-key-per-visit idempotency logic exactly. For the summary tile, the trip's
destination kind comes from GET /trips/{tripId} (already cached from the trip page).

Layout (max width 1120px, minimal header: logo only; no tab bar on mobile):
- Title row: back chevron icon button (-> /me/trips) + h1 "Confirm and pay".
- Hold banner across the top: Countdown "Your seat is held for 24:13 · until 4:32 pm"
  (Asia/Dhaka); danger styling under 5 minutes; at zero the pay button disables and the
  banner says the seat was released, with a link back to the trip.
- Left column (sections split by hairlines):
  "Your trip": Dates row (date range), Travellers row ("1 traveller"), Host row.
  "Pay with": the gateway as a single selected option row (icon + "Pay online with the
  payment gateway"), no card form (the gateway collects details).
  "Cancellation policy": the refund rule lines + "Learn more" modal.
  "How your payment is protected": a 3-step mini diagram (You pay -> Ghurify holds it in
  escrow -> Host is paid after departure).
  Failed payment (latestPaymentStatus Failed): danger Alert with latestPaymentFailure.
  Small print: by selecting the button you agree to the refund policy.
  Brand button "Confirm and pay ৳{total}" (loading "Taking you to payment…"), 56px.
- Right column: sticky summary card (e2, radius 12): Scenery thumbnail (120x100, radius 8)
  + trip title + host name; hairline; "Price details": "Trip price ৳{amount}", "Service fee
  ৳{fee}", hairline, **"Total (BDT) ৳{total}"**. Server values only.
- Mobile: the summary card moves to the top, collapsed to one line that expands; the pay
  button sits in the fixed bottom bar.
- Not Held: Confirmed -> "You've already paid" success page with "Message the group";
  otherwise "This booking can't be paid" with a link to Trips.
States: loading skeleton, error with retry, 403/404 -> not found card.
```

---

## Prompt D14: Sandbox payment gateway (`/payments/sandbox`)

```
Restyle src/features/payments/SandboxPaymentPage.tsx. It stands in for the real gateway in
development only and must look clearly unlike Ghurify, so nobody mistakes it for a real
payment page.

Data: GET /payments/sandbox/{reference} (from ?ref=) -> SandboxPayment { reference, bookingId,
total, status }; POST …/complete { succeed } -> CallbackHandled { bookingId, outcome }, then go
to /payments/result?booking=…&outcome=….

Design: plain grey page, no Ghurify header styling inside the card, a yellow-black striped
"SANDBOX - no real money" banner, reference in monospace, total, two buttons "Simulate
success" and "Simulate failure".
```

---

## Prompt D15: Booking confirmation (`/payments/result`)

```
Redesign src/features/payments/PaymentResultPage.tsx in the style of Airbnb's reservation
confirmation.

Data: query params booking and outcome; polls GET /bookings/{id}/checkout until status is
Confirmed, latestPaymentStatus is Failed/Expired, or status is Cancelled (keep the logic).
Trip details from GET /trips/{tripId} (cached).

States (max width 680px, centred):
- Confirming: a calm spinner, "Confirming your payment…", "This can take a minute. You can
  leave this page; we'll notify you."
- Confirmed: h1 "You're going to {destination}!" with a one-shot confetti that respects
  reduced motion; a trip card (Scenery tile, title, dates, host, total paid ৳{total});
  "What happens next" as 3 icon rows (group messages are open, check-ins keep everyone safe,
  the host is paid after departure); dark "Message the group" -> /trips/{tripId}/chat and
  outline "Go to Trips".
- Failed: "Your payment didn't go through", latestPaymentFailure when present, brand "Try
  again" (only while the booking is still Held, with the remaining hold time) or "Go to Trips".
```

---

## Prompt D16: Messages (`/trips/:id/chat`)

```
Redesign src/features/chat/ChatPage.tsx as Airbnb's Messages: three panes on desktop
(conversations | thread | trip details), the thread alone on mobile.

Data:
- GET /me/chats -> ChatUnread[] { tripId, title, unread }: the conversation list.
- GET /trips/{id}/chat?before={id} -> ChatHistory { messages[], pinned[] }; ChatMessageView:
  id, tripId, senderId, senderName, kind (Message|Announcement|System), body, isPinned,
  wasMasked, created. 50 per page.
- POST /trips/{id}/chat { body (max 2000), pin } -> { message, contactsMasked }.
- POST /trips/{id}/chat/read { lastReadId }. Live: /hubs/chat "message"; connection state
  connecting | live | reconnecting | offline.
- GET /trips/{id}: title, dates, destination, host, meetingPoint for the details pane.

Layout (full height under the header; no footer; no tab bar on mobile):
- Left pane 360px (>= 1128px): "Messages" h2, list rows: Scenery thumbnail (48px circle) by
  trip kind when known, trip title (600 when unread > 0), unread count badge; the active row
  has a #f7f7f7 background. Selecting navigates to /trips/{tripId}/chat. Empty: "No messages
  yet".
- Middle pane: header with trip title, "Host + travellers with a seat", connection dot (green
  live; amber "Reconnecting…" thin banner; red "Offline · Retry"), and a "Trip details" button
  (opens the right pane, or a sheet on mobile).
  Pinned bar under the header: latest pinned message with a pin icon; tap expands up to 3.
  Thread: date separators centred (Today, Yesterday, 12 Oct; Asia/Dhaka); messages left
  aligned for everyone (Airbnb style): Avatar + **name** + muted time on the first message of
  a 5-minute group, then body; the host gets a small "Host" chip (senderId == trip.host.id);
  my own messages use the same layout with "You". Announcement: full-width card with a turmeric
  left border and a megaphone icon. System: centred muted 12px text. wasMasked: lock icon with
  "Phone numbers and emails are hidden for your safety".
  "Load earlier messages" at the top keeping scroll position; "New messages ↓" pill when
  scrolled up.
  Composer: rounded 24px auto-growing field with a round send button inside on the right (dark
  when there is text), Enter sends / Shift+Enter newline on desktop, counter near 2000, host-only
  "Pin as announcement" toggle. After contactsMasked = true, a dismissible warning Alert.
- Right pane 375px (>= 1440px, else a toggle): Scenery tile, trip title, dates, meeting
  point, host row with Avatar -> /users/{host.id}, links "View trip", "Safety", "Report".
Not a member (hub join fails / 403): empty state explaining only the host and paid travellers
can message, with a link to the trip.
```

---

## Prompt D17: Trip safety (`/trips/:id/safety`)

```
Redesign src/features/safety/TripSafetyPage.tsx. Airbnb's calm visual language (white,
hairlines, 600 headings) applies, but this screen may be used in a real emergency, outdoors,
on a weak connection, on a small phone. Clarity beats beauty: huge targets, high contrast,
minimal text, one-thumb use. No tab bar on this page.

Data:
- POST /trips/{id}/sos { latitude, longitude, accuracyMeters, message (max 500) } ->
  SosRaisedView { sosId, emergencyContactTexted, nearestHelp[] { kind, name, nameBn, phone,
  latitude, longitude, distanceMeters } }.
- POST /sos/{sosId}/location { latitude, longitude } (keep sending updates).
- POST /sos/{sosId}/resolve ("I'm safe").
- GET /trips/{id}/check-ins -> CheckInView[] { id, tripId, label, dueAt, status (Scheduled|
  Done|Missed), checkedInBy, checkedInOn, note }. Host only: POST /trips/{id}/check-ins
  { label (max 150), dueAt }. Anyone on the trip: POST /check-ins/{id}/done { note }.
- GET /trips/{id} to know whether the viewer is the host.

Layout (max width 680px):
- Header: back arrow to the trip, title "Safety", trip name muted; a permanent outline danger
  "Call 999" button (tel:999) at the top right.
- SOS block: a 180px round danger (#c13515) button "SOS" with "Send an alert" under it, and
  one line on what happens (the safety desk, your host and your emergency contact get your
  location). Tap -> confirm dialog (D28). Geolocation denied or unavailable -> still allow
  sending without a position, say so, and emphasise "Call 999".
- After sending: a full-width danger panel "SOS sent. Help is being alerted.", whether the
  emergency contact was texted, a "Sharing your live location" indicator, the nearest help
  as rows (kind icon, name in the UI language, "1.2 km", round call button when phone exists,
  "Map" link via mapLink(); "Distances are approximate"), and a large dark "I'm safe now".
- Check-ins (hairline above): a timeline sorted by dueAt (Asia/Dhaka): label, due time, status
  chip (Scheduled neutral, Done success with who / when / note, Missed danger), "I'm OK" outline
  button for due ones. Host sees "Schedule a check-in" (label + date-time stacked fields,
  dark button). One line: a missed check-in alerts the safety desk.
Must work at 320px; no hover-only affordances; only a subtle pulse on the SOS state, off with
reduced motion.
```

---

## Prompt D17b: Write reviews (`/trips/:id/review`)

```
Redesign src/features/feed/ReviewPage.tsx in the style of Airbnb's post-stay review flow:
one person per screen, big stars, a progress bar, minimal header with "Exit".

Data: GET /trips/{id}/reviewable -> Reviewable[] { userId, displayName, direction
(TravelerToHost|HostToTraveler|TravelerToGuide), alreadyReviewed }.
POST /trips/{id}/reviews AddReviewCommand { revieweeId, direction, rating (1-5), body (max
1000, optional) }. GET /trips/{id} for the title and Scenery.

Flow:
- Intro screen: Scenery tile, "How was {trip title}?", "Your reviews help the next
  travellers", "Get started" dark button, list of people to review (already reviewed checked).
- One screen per person not yet reviewed: Avatar (64px), "How was {name} as a host?" /
  "...as a traveller?" from direction; 5 large star buttons (40px, aria radio group, arrow
  keys, "4 out of 5" labels, a word under the selected value: Terrible … Amazing); optional
  text "Tell future travellers about {name}" with counter; "Skip" text link and dark "Submit".
- Bottom segmented progress bar ("2 of 4").
- Final screen: "Thanks for your reviews" + brand "Share a story" -> /feed + "Back to Trips".
Empty: "Nobody to review on this trip".
```

---

## Prompt D18: Hosting dashboard (`/host/trips`)

```
Redesign src/features/trips/HostTripsPage.tsx in the style of Airbnb's hosting "Today"
page plus its Listings table.

Data: GET /me/trips -> HostTripSummary[]: id, title, destination { slug, name, nameBn, kind,
status }, startDate, endDate, seats, seatsTaken, pricePerPerson, groupType, status
(Draft|Published|Full|Cancelled|Completed), pendingRequests. Publish: POST /trips/{id}/publish.
Cancel: POST /trips/{id}/cancel. Name from GET /me/profile.

Layout (max width 1280px):
- Hosting header mode: the user menu shows "Switch to travelling"; nav tabs "Today",
  "Trips", "Earnings" (-> /host/payouts) as text tabs in the header area.
- "Welcome back, {displayName}" (32px 600) and a brand "Create a trip" button on the right.
- "Your reservations" (Airbnb's tab row with counts, computed from the list and dates in
  Asia/Dhaka): Needs your reply (pendingRequests > 0), Happening now (start <= today <= end),
  Starting soon (next 14 days), Upcoming, Drafts. Under the tabs, horizontally scrollable
  cards (radius 12, hairline): status line ("3 requests to review" in turmeric, "Starts in 4
  days", "Draft"), title, dates, seats "6 of 10 filled" with a thin bar, and the main action
  ("Review requests", "Message group", "Finish draft"). Empty tab: muted line in a grey box,
  like Airbnb's "You don't have any guests checking out today".
- "Your trips" table (Airbnb Listings): columns Trip (Scenery thumb 56px radius 8 + title +
  destination), Status (dot + label), Dates, Seats (taken / total), Price, Requests (badge),
  and a "…" actions menu. Card list on mobile. Filters: status chips (All, Published, Full,
  Draft, Completed, Cancelled). Row actions by status (keep these rules):
  Draft: Edit, Publish, Cancel. Published/Full: Requests, Messages, Safety, Edit, Cancel.
  Completed: Messages. Cancelled: none.
- Cancel uses ConfirmDialog: everyone who paid is refunded in full, fees included.
- Publish failing with 403 -> Alert linking to /account/verify (needs ID + selfie).
Empty state: Scenery tile, "Lead your first trip", 3 short tips, brand "Create a trip".
```

---

## Prompt D19: Trip creation flow (`/host/trips/new`, `/host/trips/:id/edit`)

```
Redesign src/features/trips/TripWizardPage.tsx as Airbnb's "become a host" flow: full-screen,
one question per screen, big type, minimal chrome. Keep the zod schema and validation; the 4
logical steps (basics, costs, itinerary, review) stay, split into smaller screens. Each screen
validates only its own fields with trigger() before moving on.

Data: SaveTripCommand { destinationSlug, title (5-150), summary (20-1000), startDate (after
today in Dhaka), endDate (>= start; trip <= 30 days), meetingPoint (3-200), seats (1-50),
pricePerPerson, groupType, costItems[] (1-20) { category, description (<=150), amount
(0-1,000,000) }, itinerary[] (one per day) { dayNo, title (<=150), details (<=1000),
difficulty } }. POST /trips -> { id }; PUT /trips/{id} -> TripDetail; POST /trips/{id}/publish.
Destinations from GET /destinations (Closed disabled with their status).

Chrome: header with the logo left and "Questions?" + "Save & exit" outline pills on the right
(Save & exit saves a draft and returns to /host/trips). Footer fixed: a 3-segment progress bar
across the top edge (Basics / Costs / Itinerary, filling as screens complete), "Back" text
link left, dark "Next" button right (brand "Publish" on the last screen).

Screens (content centred, max width 630px, h1 32px 600):
1. Intro "Step 1: Tell us about your trip" with a Scenery tile (Airbnb's step intro pattern).
2. "Where is your trip going?": grid of destination tiles (Scenery by kind, name, status chip),
   selected = 2px ink border.
3. "Who is this trip for?": four large option cards with icons (Open, Women only, Students,
   Families) and a line each; women only explains who can see and join it.
4. "When is it?": a two-month calendar for the date range; "3 days" shown live.
5. "Give your trip a title" (counter 150) and "Describe the trip" (counter 1000, tips).
6. "Where will everyone meet?" meeting point field, and "How many seats?" a large stepper.
7. Intro "Step 2: Set a fair, clear price".
8. "Break down the cost": editable rows (category select with icon, description, amount),
   add / remove, live stacked bar, the price per person shown huge (48px) at the top as Airbnb
   does on its price screen. Hint: travellers trust itemised prices.
9. Intro "Step 3: Plan each day".
10. "Plan the days": one collapsible card per trip day (from the dates, max 30): "Day 1 · Sat
    12 Oct", title, details, difficulty segmented control.
11. "Review your trip": left the TripCard exactly as travellers will see it (with a "Show
    preview" link to the full listing in a modal), right "What's next" with 3 icon rows
    (publish, review requests, travellers pay into escrow). If not NidSelfie verified, an Alert
    links to /account/verify instead of a publish that fails. Buttons: "Save draft" outline and
    brand "Publish".
Edit mode: same screens; the header says "Edit trip" and the final button is "Save changes".
Warn on leaving with unsaved changes. Server validation errors jump to the screen that owns
the field.
```

---

## Prompt D20: Join requests (`/host/trips/:id/requests`)

```
Redesign src/features/bookings/ManageRequestsPage.tsx in the style of Airbnb's reservation
request review: who is asking, what they said, and two clear choices.

Data: GET /trips/{id}/join-requests -> JoinRequestForHost[]: id, userId, displayName, gender,
verifiedLevel, message, status (Pending|Approved|Declined|Expired|Cancelled), created,
bookingId, bookingStatus (Held|Confirmed|Cancelled|Refunded), holdExpiresAt.
Approve: POST /join-requests/{id}/approve -> { bookingId, holdExpiresAt }.
Decline: POST /join-requests/{id}/decline. Trip from GET /trips/{id} (title, dates, seats,
seatsLeft, groupMix, groupType).

Layout (max width 1120px):
- Back chevron + h1 "Requests" + trip title and dates muted; on the right a summary card:
  "4 of 10 seats left", seat dots, and the current group mix.
- Tabs: Pending (count), Awaiting payment (Held), Confirmed, Other.
- Request card (radius 12, hairline): Avatar (64px), **name** -> /users/{userId} (new tab, so
  the host can read their reviews), verified shield + level, gender when not Unspecified,
  "Requested 2 hours ago"; their message as a quote block; on the right (below on mobile):
  dark "Approve" and outline "Decline" (ConfirmDialog). Disable Approve when seatsLeft is 0,
  with the reason. Held: Countdown + "Waiting for payment". Confirmed: success chip.
- After approve, toast "Seat held for 30 minutes while they pay".
Empty Pending: "No requests yet" + "Share your trip" with a copy-link button.
```

---

## Prompt D21: Earnings (`/host/payouts`)

```
Redesign src/features/payments/HostPayoutsPage.tsx in the style of Airbnb's Earnings page:
a large headline number, then transaction history.

Data: GET /me/payouts -> PayoutView[]: id, tripId, tripTitle, startDate, hostId, hostName,
stage (BeforeDeparture|AfterStart), amount, platformAmount, status (Released|Paid), created,
approvedOn.

Layout (max width 1120px):
- h1 "Earnings"; headline "You've been paid ৳{sum of Paid}" (40px 600) and a muted line
  "৳{sum of Released} on its way". Totals are computed from the list.
- A simple bar chart of paid amounts by month (approvedOn, Asia/Dhaka), last 6 months, built
  with plain SVG (no chart package), only when there is data.
- "How payouts work" card: escrow -> part released before departure (BeforeDeparture) -> rest
  after the trip starts (AfterStart) -> Ghurify sends it -> Paid.
- "Transaction history" with tabs "Paid" and "On its way": rows grouped by trip (title ->
  /trips/{tripId}, start date): stage label, amount, status chip, date (approvedOn or created),
  "Ghurify fee ৳{platformAmount}" muted. Table on desktop, rows on mobile.
Empty: "No earnings yet" + "Payouts start when travellers pay for your trips".
Payout account details are not in the API; do not design a bank or mobile-wallet form.
```

---

## Prompt D22: Admin shell and overview (`/admin`)

```
Redesign src/features/admin/AdminLayout.tsx and AdminDashboardPage.tsx. Internal tool: use
Airbnb's hosting-dashboard restraint (white, hairlines, 600 headings, dense tables), tighter
spacing, keyboard-friendly.

Shell: left sidebar (240px, collapsible to icons) on desktop, a select on mobile. Sections by
role (keep the existing map): Overview (Admin, SafetyDesk, Moderator), Verifications (Admin),
Reports (Admin, Moderator), Disputes (Admin), Destinations (Admin, SafetyDesk), SOS (Admin,
SafetyDesk), Payouts (Admin). Each item shows its live count from the dashboard. A danger pill
"N open SOS" in the shell header on every admin page when openSos > 0.

Overview: GET /admin/dashboard -> DashboardCounts: pendingVerifications, openReports,
openDisputes, openSos, missedCheckIns, payoutsAwaitingApproval, refundsInFlight,
closedDestinations, cautionDestinations, liveTrips, bookingsConfirmed. Refetch every 30s.
Three groups of stat tiles (number 32px 600, label muted, radius 12, hairline):
- Needs attention now (danger when > 0): open SOS, missed check-ins today -> /admin/sos.
- Queues: verifications, reports, disputes, payouts to send -> their pages.
- Platform: live trips, bookings confirmed today, refunds in progress, destinations on caution
  and closed -> /admin/destinations.
Tiles link only for roles that can open the target (keep current logic). Zero = quiet "All
clear". Show "Updated 20 s ago".
```

---

## Prompt D23: Verification queue (`/admin/verifications`)

```
Redesign src/features/admin/VerificationQueuePage.tsx.

Data: GET /admin/verifications?status=Pending|Approved|Rejected&page=N -> { items[],
totalCount, page, pageSize }; item: id, userId, displayName, maskedEmail, level, status,
provider, providerRef, reason, created. Review: POST /admin/verifications/{id}/review
{ approve, reason (max 300) }.

Layout: segmented control (Pending / Approved / Rejected), a dense table (Person: Avatar +
name + masked email; Level chip; Provider + providerRef in monospace; Submitted relative time;
Status; Actions), cards on mobile. Approve is one click; Reject expands an inline reason field
(required). Remove the row from Pending on success with a toast. Airbnb-style numbered
pagination with totalCount. Empty: "No verifications waiting".
NID numbers are never shown (the API does not have them).
```

---

## Prompt D24: Reports and disputes (`/admin/reports`, `/admin/disputes`)

```
Redesign src/features/admin/ReportsQueuePage.tsx (one component, `disputes` prop) as an
Airbnb Messages-like master-detail view.

Data: GET /admin/reports?kind=User|Post|Trip|Dispute -> ReportView[]: id, reporterId,
reporterName, kind, targetId, reason (Harassment|Fraud|Unsafe|Inappropriate|Payment|Other),
details, status (Open|Actioned|Dismissed), resolution, created.
Resolve: POST /admin/reports/{id}/resolve { action (Dismiss|HidePost|SuspendUser|RefundBooking|
Resolve), resolution (max 500, required) }.

Layout: list pane left (400px) and detail pane right; on mobile the list, then a detail page.
List rows: reason chip (Harassment, Fraud and Unsafe in danger), kind, target ("Trip #12" ->
/trips/12, "User #8" -> /users/8, "Post #31", "Booking #44" for disputes), reporter, age; the
selected row on #f7f7f7. Kind filter chips on the reports page (User, Post, Trip).
Detail: details text, reporter -> /users/{reporterId}, target link, then the action picker
showing only actions that fit (HidePost for Post, SuspendUser for User, RefundBooking for
Dispute, Dismiss and Resolve always), a required resolution note with counter, and a
ConfirmDialog for SuspendUser and RefundBooking ("This is audited and can't be undone").
Empty: "No open reports".
```

---

## Prompt D25: Destination safety status (`/admin/destinations`)

```
Redesign src/features/admin/DestinationAlertsPage.tsx.

Data: GET /destinations (status, statusNote, statusNoteBn, upcomingTrips). Change: POST
/admin/destinations/{slug}/status { status (Open|Caution|Closed), note, noteBn } (max 300 each).

Layout: a table (Scenery thumb, name en + bn, division, kind, status chip, current note,
upcoming trips), status filter chips, cards on mobile. "Change status" opens a D0 modal:
three large option cards (Open, Caution, Closed) with consequences; English and Bangla note
fields side by side with a live preview of the Alert travellers will see; Closed shows a
danger warning that every booked traveller on the {upcomingTrips} upcoming trips there is
refunded in full and notified, and requires a confirm checkbox before the dark "Save" button
enables. Keep Leaflet out of this page.
```

---

## Prompt D26: Live SOS board (`/admin/sos`)

```
Redesign src/features/admin/SosBoardPage.tsx for the safety desk: a live operations screen,
often on a large monitor, read at a glance. Airbnb restraint, but emergency-first.

Data: GET /admin/sos?includeResolved=bool -> SosBoardItem[]: id, userId, userName, userPhone,
tripId, tripTitle, latitude, longitude, message, status (Open|Acknowledged|Resolved), created,
lastSeenOn, hostName, hostPhone. GET /admin/check-ins/missed -> MissedCheckIn[] { checkInId,
tripId, label, hostId, tripTitle }. Acknowledge: POST /admin/sos/{id}/acknowledge. Resolve:
POST /sos/{id}/resolve. Live: /hubs/safety "sos" and "checkInMissed" (keep refetch-on-event
plus the interval fallback). Show the hub connection state.

Layout:
- Header: live dot, counts (Open, Acknowledged), "Show resolved" switch.
- Cards in a responsive grid, Open first (oldest first), danger left border and a gentle pulse
  until acknowledged; Acknowledged amber; Resolved muted. Each card: traveller name (20px 600)
  + tel: link for userPhone, trip title -> /trips/{tripId}, host name + tel: link for hostPhone,
  message, "Raised 4:12 pm" and "Last seen 2 min ago" (danger when older than 10 min),
  coordinates in monospace with copy and "Open map" (mapLink). Actions: dark "Acknowledge"
  (Open only), outline "Resolve".
- Missed check-ins side panel: label, trip link, "Contact host".
- Optional audible alert on a new Open SOS, off by default, remembered per viewer.
Very high contrast, large names and phone numbers; works at 1920px and 375px.
```

---

## Prompt D27: Payout queue (`/admin/payouts`)

```
Redesign src/features/admin/PayoutQueuePage.tsx.

Data: GET /admin/payouts?status=Released|Paid -> PayoutView[] (fields in D21, plus hostId,
hostName). Approve (marks as sent, audited): POST /admin/payouts/{id}/approve.

Layout: segmented control (To send = Released / Sent = Paid); table with Host (-> /users/
{hostId}), Trip (-> /trips/{tripId}) + start date, Stage, Amount, Ghurify fee (platformAmount),
Created, Sent on (approvedOn); total of amounts in the footer. "Mark as sent" opens a
ConfirmDialog with host, amount and stage. Cards on mobile. Empty: "No payouts to send".
```

---

## Prompt D28: Dialogs (join, cancel booking, report, SOS confirm)

```
Restyle the four dialogs on the D0 modal (Airbnb style: X top-left, centred title, hairline,
sticky footer; full-height sheet on mobile).

1. JoinRequestDialog (features/bookings), titled "Request to join": POST /trips/{id}/join-
   requests { message (max 500, optional) } -> { id }. Top: a compact trip row (Scenery thumb,
   title, dates, ৳ per person). "Message the host" stacked textarea with counter and tappable
   example chips ("First time in the hills", "Travelling with a friend"). A 3-step "What
   happens next" (the host reviews -> you get 30 minutes to pay -> you're in). Footer: brand
   "Send request" and muted "You won't be charged yet". 403 -> warning Alert linking to
   /account/verify. Success: check illustration, "Request sent", dark "Go to Trips".
2. CancelBookingDialog (features/payments), titled "Cancel your booking": GET /bookings/{id}/
   cancellation -> CancellationQuote { bookingId, canCancel, daysBeforeDeparture, paid, refund,
   rule (full|half|none|not_cancellable) }; POST /bookings/{id}/cancel. Body: "12 days before
   departure", a two-row price table "You paid ৳{paid}" / "You'll get back ৳{refund}", the rule
   sentence. Footer: outline "Keep my booking" (default focus) and danger "Cancel and get
   ৳{refund} back".
3. ReportDialog (features/safety), titled "Report this {kind}" or "Report a problem": POST
   /reports { kind (User|Post|Trip|Dispute), targetId, reason, details (max 1000) }. Airbnb's
   report flow: screen 1 radio rows for the reason with one-line descriptions, "Next"; screen 2
   details with counter, "Submit"; a note that reports are private; success screen.
4. SOS confirm (inside TripSafetyPage): title "Send SOS?", optional message (max 500), a very
   large danger "Send SOS now" button and a clearly secondary "Cancel"; never auto-dismiss and
   Esc does nothing while sending.
All dialogs: focus trap, errors inline, loading dots in the busy button.
```

---

## Gaps found while mapping the API (decide before or during the prompts)

These are not design work, but the Airbnb patterns run into them. None of the prompts builds
them.

1. **Airbnb patterns the API cannot support yet**
   - Category bar filtering trip search: needs a `Kind` parameter on `GET /trips`. Until then it
     only filters destinations on the home page (D2).
   - Ratings on trip cards: needs `hostRating` / `hostReviewCount` on `TripSummary`.
   - Photo grids and carousels: needs trip and destination media.
   - Wishlists (heart button): no saved-trips API.
   - Avatars: no profile photo field.
   - Kind for `PublicProfile.hostedTrips`: D8 looks it up from `GET /destinations`.
2. **Endpoints with no screen yet**
   - `GET /admin/audit` (entityType, entityId) -> an admin audit log page.
   - `POST /admin/users/{id}/roles` { role, grant } -> admin role management.
   - `DELETE /comments/{id}` -> deleting your own comment.
   - A full `/me/notifications` page (the API already returns the list).
3. **Search parameters the UI did not use before**: `To` and `MinSeats`. D1, D2 and D3 add them.
4. **Guides / marketplace**: `features/guides` is empty and there is no API yet.
