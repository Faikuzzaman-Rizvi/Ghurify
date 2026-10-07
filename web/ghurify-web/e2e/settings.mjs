// Settings shared by the end-to-end prepare step and the Playwright config.
//
// The end-to-end run never touches your development data: it uses its own database
// (GhurifyE2E) on the local SQL Server container, its own blob containers on Azurite, its own
// API on port 5299 and its own web server on 5174. The password is the local-only one from
// docker-compose.yml.
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const here = path.dirname(fileURLToPath(import.meta.url));

export const repoRoot = path.resolve(here, '..', '..', '..');
export const apiPort = 5299;
export const webPort = 5174;
export const apiUrl = `http://localhost:${apiPort}`;
export const webUrl = `http://localhost:${webPort}`;
export const databaseName = 'GhurifyE2E';
export const sqlContainer = process.env.E2E_SQL_CONTAINER ?? 'ghurify-sqlserver';
export const saPassword = process.env.E2E_SA_PASSWORD ?? 'Ghurify_Local_Dev_1';
export const connectionString =
  process.env.E2E_DB ??
  `Server=localhost,1433;Database=${databaseName};User Id=sa;Password=${saPassword};TrustServerCertificate=True;Encrypt=False;`;
export const mailDirectory = path.join(here, '.mail');
// "fake" (offline, the default) or "sslcommerz": pay on the real SSLCommerz sandbox page with
// its public test store and test card. Needs internet access.
export const paymentsProvider = process.env.E2E_PAYMENTS === 'sslcommerz' ? 'sslcommerz' : 'fake';
