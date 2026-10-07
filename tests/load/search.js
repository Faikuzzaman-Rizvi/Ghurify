// k6 load test: 5,000 people browsing and searching trips at once.
//
//   k6 run tests/load/search.js                                  # against http://localhost:5199
//   k6 run -e API=https://staging-api.ghurify.app tests/load/search.js
//
// Anonymous traffic only (search and trip pages are public), so it needs no accounts and writes
// nothing. Run it against staging, never production: 5,000 virtual users is a real load.
// The thresholds fail the run when search p95 goes over 800 ms or more than 1% of requests fail.
//
// One load generator is one IP, and the API allows 300 requests a minute per IP. Start the API
// under test with RateLimits__GlobalPerMinute=1000000 (staging only), or every request past the
// first few hundred is a 429 and the run measures the rate limiter, not the search.
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';

const API = __ENV.API || 'http://localhost:5199';

const searchLatency = new Trend('search_latency', true);
const tripLatency = new Trend('trip_latency', true);

export const options = {
  scenarios: {
    browsing: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '1m', target: 1000 },
        { duration: '2m', target: 5000 },
        { duration: '3m', target: 5000 },
        { duration: '1m', target: 0 },
      ],
      gracefulRampDown: '30s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    search_latency: ['p(95)<800'],
    trip_latency: ['p(95)<600'],
  },
};

// The searches people actually make: by destination, by date, by price, women-only, and plain.
const queries = [
  '',
  '?destination=sajek',
  '?destination=coxs-bazar&sort=PriceLowToHigh',
  '?destination=bandarban&groupType=WomenOnly',
  '?maxPrice=8000',
  '?verifiedHostsOnly=true',
  '?minSeats=2',
  '?page=2',
];

export function setup() {
  const destinations = http.get(`${API}/api/v1/destinations`);
  check(destinations, { 'destinations load': (r) => r.status === 200 });
  return {};
}

export default function () {
  const query = queries[Math.floor(Math.random() * queries.length)];
  const search = http.get(`${API}/api/v1/trips${query}`, { tags: { name: 'search' } });
  searchLatency.add(search.timings.duration);
  check(search, { 'search 200': (r) => r.status === 200 });

  // Most people open a trip or two from the results.
  if (search.status === 200) {
    const items = search.json('items') || [];
    if (items.length > 0 && Math.random() < 0.6) {
      const trip = items[Math.floor(Math.random() * items.length)];
      const detail = http.get(`${API}/api/v1/trips/${trip.id}`, { tags: { name: 'trip' } });
      tripLatency.add(detail.timings.duration);
      check(detail, { 'trip 200': (r) => r.status === 200 });
    }
  }

  // Think time: people read before they click again.
  sleep(1 + Math.random() * 3);
}
