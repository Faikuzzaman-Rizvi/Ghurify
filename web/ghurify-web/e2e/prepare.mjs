// Prepares a clean end-to-end environment, then exits. Run by `npm run test:e2e` before Playwright.
//
//   1. drops and recreates the GhurifyE2E database with the schema, data scripts and demo data
//      (demo hosts sign in with the password Ghurify-demo-2026);
//   2. empties the folder the API writes account emails to;
//   3. builds the API, so Playwright can start it without building.
//
// Needs Docker (the sqlserver and azurite services from docker-compose.yml), PowerShell and
// sqlpackage, the same as Publish-Database.ps1.
import { execFileSync } from 'node:child_process';
import { mkdirSync, rmSync } from 'node:fs';
import path from 'node:path';
import {
  connectionString,
  databaseName,
  mailDirectory,
  repoRoot,
  saPassword,
  sqlContainer,
} from './settings.mjs';

const run = (command, args) => execFileSync(command, args, { stdio: 'inherit', cwd: repoRoot });

console.log(`Recreating the ${databaseName} database...`);
run('docker', [
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
  '-Q',
  `IF DB_ID('${databaseName}') IS NOT NULL BEGIN ALTER DATABASE [${databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${databaseName}]; END; CREATE DATABASE [${databaseName}];`,
]);

run('powershell', [
  '-NoProfile',
  '-ExecutionPolicy',
  'Bypass',
  '-File',
  path.join(repoRoot, 'Publish-Database.ps1'),
  '-ConnectionString',
  connectionString,
  '-Demo',
]);

rmSync(mailDirectory, { recursive: true, force: true });
mkdirSync(mailDirectory, { recursive: true });

console.log('Building the API...');
run('dotnet', [
  'build',
  path.join(repoRoot, 'src', 'Ghurify.Api'),
  '-c',
  'Release',
  '-nologo',
  '-v',
  'q',
]);
