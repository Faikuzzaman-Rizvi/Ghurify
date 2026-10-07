import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { expect, test, type Browser, type Page } from '@playwright/test';
import {
  databaseName,
  mailDirectory,
  paymentsProvider,
  saPassword,
  sqlContainer,
  webUrl,
} from './settings.mjs';

/**
 * The core journey, in a real browser against the real API and database, at phone width:
 * create an account (one emailed code) -> complete the profile -> verify with ID photos ->
 * find a trip -> ask to join -> the host approves -> pay into escrow (sandbox) -> chat with the
 * group -> after the trip, review the host.
 */

const password = 'monsoon tea garden walk';
const hostEmail = 'rafiq.chowdhury@demo.ghurify.app';
const hostPassword = 'Ghurify-demo-2026';
const tripTitle = 'Nilgiri and Boga Lake trek';

test('a new traveller signs up, verifies, books, pays, chats and reviews', async ({ browser }) => {
  const suffix = Date.now().toString(36);
  const email = `traveller-${suffix}@ghurify.test`;
  const traveller = await openAs(browser);

  // 1. Create an account, confirm the email with the code, and land signed in.
  await traveller.goto('/register');
  await traveller.getByLabel('Your full name').fill('Mitu Akter');
  await traveller.getByLabel('Email address').fill(email);
  await traveller.getByLabel('Password', { exact: true }).fill(password);
  await traveller.getByRole('button', { name: 'Create account' }).click();
  await traveller.getByLabel('Six-digit code').fill(await codeFor(email));
  await traveller.getByRole('button', { name: 'Confirm and continue' }).click();
  await expect(traveller).toHaveURL(/\/$/);

  // 2. The profile: gender (women-only trips, the ID check) and a phone (needed to pay).
  await traveller.goto('/account');
  await traveller.getByLabel('Gender').selectOption('Female');
  await traveller
    // Two fields are labelled 'Mobile number' (yours and your emergency contact's).
    .locator('#phone')
    .fill(`0171${Math.floor(1_000_000 + Math.random() * 8_999_999)}`);
  await traveller.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(traveller.getByText('Profile saved.')).toBeVisible();

  // 3. Verify with photos of both sides of the NID. The development e-KYC approves at once.
  await traveller.goto('/account/verify');
  await traveller
    .getByLabel('National ID number')
    .fill(`19${Math.floor(10_000_000 + Math.random() * 89_999_998)}`);
  await traveller.getByLabel('Date of birth').fill('1995-04-12');
  await traveller.getByLabel('Front of the NID').setInputFiles(photo('front.jpg'));
  await expect(traveller.getByLabel('Photo accepted')).toHaveCount(1);
  await traveller.getByLabel('Back of the NID').setInputFiles(photo('back.jpg'));
  await expect(traveller.getByLabel('Photo accepted')).toHaveCount(2);
  await traveller.getByRole('button', { name: 'Check my ID' }).click();
  await expect(traveller.getByText('You are verified.')).toBeVisible();

  // 4. Find the trip and ask to join.
  await traveller.goto('/trips?destination=bandarban');
  await traveller
    .getByRole('link', { name: new RegExp(tripTitle) })
    .first()
    .click();
  const tripId = Number(/\/trips\/(\d+)/.exec(traveller.url())?.[1]);
  expect(tripId).toBeGreaterThan(0);
  await traveller.getByRole('button', { name: 'Request to join' }).click();
  await traveller
    .getByLabel('A note for the host (optional)')
    .fill('First trek, but I walk every morning.');
  await traveller.getByRole('button', { name: 'Send request' }).click();
  await expect(traveller.getByText('Request sent.')).toBeVisible();

  // 5. The host signs in with their password (no code) and approves.
  const host = await openAs(browser);
  await host.goto('/login');
  await host.getByLabel('Email address').fill(hostEmail);
  await host.getByLabel('Password', { exact: true }).fill(hostPassword);
  await host.getByRole('button', { name: 'Sign in' }).click();
  await expect(host).toHaveURL(/\/$/);
  await host.goto(`/host/trips/${tripId}/requests`);
  await host.getByRole('button', { name: 'Approve and hold a seat' }).click();
  await expect(host.getByRole('button', { name: 'Approve and hold a seat' })).toHaveCount(0);

  // 6. Pay into escrow: on the built-in pretend page, or the real SSLCommerz sandbox
  //    (E2E_PAYMENTS=sslcommerz), which sends the traveller back through the API's return URL.
  await traveller.goto('/me/trips');
  await traveller.getByRole('link', { name: /^Pay / }).first().click();
  await traveller.getByRole('button', { name: /^Pay / }).click();
  if (paymentsProvider === 'sslcommerz') {
    await payWithSslCommerzTestCard(traveller);
  } else {
    await traveller.getByRole('button', { name: 'Pay successfully' }).click();
  }
  await expect(traveller.getByText('You are going!')).toBeVisible({ timeout: 30_000 });

  // 7. Say hello to the group.
  await traveller.goto(`/trips/${tripId}/chat`);
  await traveller
    .getByPlaceholder('Write to the group...')
    .fill('Hello everyone, see you at Sayedabad!');
  await traveller.getByRole('button', { name: 'Send' }).click();
  await expect(traveller.getByText('Hello everyone, see you at Sayedabad!')).toBeVisible();

  // 8. The trip happens (moved into the past here), and the traveller reviews the host.
  finishTrip(tripId);
  await traveller.goto(`/trips/${tripId}/review`);
  await traveller.getByRole('button', { name: '5 out of 5' }).first().click();
  await traveller.getByRole('button', { name: 'Submit review' }).first().click();
  await expect(traveller.getByText(/Thank you\. Your review of/).first()).toBeVisible();
});

/**
 * On SSLCommerz's sandbox checkout: their published test VISA card. The PAY button only enables
 * once the sandbox has looked the card up, so the click waits for it.
 */
async function payWithSslCommerzTestCard(page: Page) {
  await page.waitForURL(/sandbox\.sslcommerz\.com/, { timeout: 30_000 });
  await page.locator('#ccnum').pressSequentially('4111111111111111');
  await page.locator('#expiry').pressSequentially('1230');
  await page.locator('input[name=cvc]').pressSequentially('111');
  await page.locator('input[name=name]').pressSequentially('Mitu Akter');
  await page.keyboard.press('Tab');
  await page.locator('button.loading-btn').click({ timeout: 45_000 });

  // Larger amounts go through the sandbox's OTP simulator: any code, then "Success".
  const result = new RegExp(`^${webUrl}/payments/result`);
  const otp = page.getByRole('button', { name: 'Success', exact: true });
  await expect(otp.or(page.getByText('You are going!'))).toBeVisible({ timeout: 60_000 });
  if (!result.test(page.url())) {
    await page
      .locator('input[type=text], input[type=password], input:not([type])')
      .first()
      .fill('111111');
    await otp.click();
  }
  await page.waitForURL(result, { timeout: 60_000 });
}

/** A fresh browser context in English, with the first-visit tour already seen. */
async function openAs(browser: Browser): Promise<Page> {
  // A context made by hand does not inherit the project's settings, so they are repeated here:
  // the smallest phone the app supports.
  const context = await browser.newContext({
    baseURL: webUrl,
    viewport: { width: 360, height: 780 },
    isMobile: true,
    hasTouch: true,
    locale: 'en-GB',
    timezoneId: 'Asia/Dhaka',
  });
  // A string, not a function: it runs in the page, which this file is not type-checked for.
  await context.addInitScript(
    "window.localStorage.setItem('ghurify.language', 'en'); window.localStorage.setItem('ghurify.tourSeen', '1');",
  );
  return context.newPage();
}

/** The newest code the API "emailed" to an address (it writes them to e2e/.mail in this run). */
async function codeFor(email: string): Promise<string> {
  const file = path.join(mailDirectory, `${email}.json`);
  await expect.poll(() => existsSync(file), { timeout: 15_000 }).toBe(true);
  const message = JSON.parse(readFileSync(file, 'utf8')) as { code?: string };
  expect(message.code).toMatch(/^\d{6}$/);
  return String(message.code);
}

/**
 * A photo of an ID card for the upload: a minimal, valid JPEG. Real photos are resized in the
 * browser first; this one is too small to decode, so it is uploaded as it is and the server's
 * checks (a real JPEG, within the size limit, metadata removed) still run.
 */
function photo(name: string) {
  const segment = (marker: number, payload: number[]) => [
    0xff,
    marker,
    (payload.length + 2) >> 8,
    (payload.length + 2) & 0xff,
    ...payload,
  ];
  const bytes = [
    0xff,
    0xd8,
    ...segment(0xe0, [0x4a, 0x46, 0x49, 0x46, 0x00, 1, 1, 0, 0, 1, 0, 1, 0, 0]),
    ...segment(0xdb, new Array<number>(65).fill(1)),
    0xff,
    0xda,
    0x00,
    0x04,
    0x01,
    0x02,
    0x10,
    0x20,
    0xff,
    0xd9,
  ];
  return { name, mimeType: 'image/jpeg', buffer: Buffer.from(bytes) };
}

/** Moves the trip into the past and marks it completed, as the hourly job would after it ends. */
function finishTrip(tripId: number) {
  execFileSync('docker', [
    'exec',
    sqlContainer,
    '/opt/mssql-tools18/bin/sqlcmd',
    '-S',
    'localhost',
    '-U',
    'sa',
    '-P',
    saPassword,
    '-C',
    '-b',
    // Filtered indexes need QUOTED_IDENTIFIER ON, which sqlcmd leaves off unless asked.
    '-I',
    '-d',
    databaseName,
    '-Q',
    `UPDATE [Main].[Trip] SET [StartDate] = DATEADD(DAY, -6, CAST(SYSUTCDATETIME() AS DATE)), [EndDate] = DATEADD(DAY, -2, CAST(SYSUTCDATETIME() AS DATE)), [Status] = 5 WHERE [Id] = ${tripId};`,
  ]);
}
