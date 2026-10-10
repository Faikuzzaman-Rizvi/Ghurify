<#
.SYNOPSIS
    Runs the whole site from one process, reachable from any device on the Wi-Fi and,
    with -Ngrok, from a public URL.

.DESCRIPTION
    Builds the web app, copies it into the API's wwwroot, and starts the API bound to every
    network interface. The site and the API then share one origin, which is what makes this work
    from another device:

      * nothing to add to the CORS list when the address changes (there is no cross-origin call),
      * the sign-in cookie is first-party, so a reload keeps you signed in,
      * SignalR needs no separate host, and
      * one port to open, one URL to type or tunnel.

    This is a hosting mode, not a change to how Ghurify deploys. Deployed, the web app is an
    Azure Static Web App on its own domain and the API is API-only (docs/RUNBOOK.md); that is
    untouched, because Hosting:ServeWebApp is off unless something turns it on, as this does.

    The database, blob storage and SMTP settings come from .env exactly as they do for
    'dotnet run', so this serves the same data a developer already sees.

.PARAMETER Port
    Port to listen on. Defaults to 5199, the port the API already uses.

.PARAMETER Configuration
    Build configuration for the API. Release by default: this is the mode meant for showing
    the site to other people, and Debug costs roughly a third of the throughput.

.PARAMETER SkipWebBuild
    Reuses the existing web/ghurify-web/dist. Useful when only backend code changed.

.PARAMETER Ngrok
    Also starts an ngrok tunnel and prints the public URL. Needs ngrok on PATH and an
    authtoken configured once ('ngrok config add-authtoken <token>').

.PARAMETER Environment
    ASPNETCORE_ENVIRONMENT. Development by default, so .env is read and the local
    appsettings.Development.json defaults apply.

.EXAMPLE
    # Reachable from any phone or laptop on the same Wi-Fi
    .\Serve-Site.ps1

.EXAMPLE
    # ...and from anywhere, through a public URL
    .\Serve-Site.ps1 -Ngrok

.EXAMPLE
    # Backend-only change: skip the web build
    .\Serve-Site.ps1 -SkipWebBuild
#>
param(
    [int]$Port = 5199,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$SkipWebBuild,

    [switch]$Ngrok,

    [string]$Environment = "Development"
)

$ErrorActionPreference = "Stop"

$RepoRoot = $PSScriptRoot
$WebDir   = Join-Path $RepoRoot "web\ghurify-web"
$ApiProj  = Join-Path $RepoRoot "src\Ghurify.Api"
$WebRoot  = Join-Path $ApiProj "wwwroot"

function Write-Step($message) {
    Write-Host ""
    Write-Host "==> $message" -ForegroundColor Cyan
}

# --- The address other devices will use -------------------------------------------------------
# The interface that actually carries traffic to the internet, which is the one a phone on the
# same Wi-Fi shares. Picked this way rather than by taking the first IPv4 address, because a
# machine with Hyper-V, WSL or Docker has several and most of them are unreachable from the LAN.
function Get-LanAddress {
    $route = Get-NetRoute -DestinationPrefix "0.0.0.0/0" -ErrorAction SilentlyContinue |
        Sort-Object -Property RouteMetric, ifMetric |
        Select-Object -First 1

    if ($route) {
        $address = Get-NetIPAddress -InterfaceIndex $route.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue |
            Where-Object { $_.IPAddress -notlike "169.254.*" } |
            Select-Object -First 1

        if ($address) { return $address.IPAddress }
    }

    # Nothing routable found (no network, or an unusual setup): the site still works locally.
    return "localhost"
}

$LanIp = Get-LanAddress

# --- 1. Build the web app ---------------------------------------------------------------------
if (-not $SkipWebBuild) {
    Write-Step "Building the web app (production build)"

    Push-Location $WebDir
    try {
        if (-not (Test-Path "node_modules")) {
            Write-Host "node_modules is missing; installing." -ForegroundColor Yellow
            npm ci
            if ($LASTEXITCODE -ne 0) { throw "npm ci failed." }
        }

        npm run build
        if ($LASTEXITCODE -ne 0) { throw "The web build failed." }
    }
    finally {
        Pop-Location
    }
}

$Dist = Join-Path $WebDir "dist"
if (-not (Test-Path (Join-Path $Dist "index.html"))) {
    throw "No built web app at $Dist. Run without -SkipWebBuild."
}

# --- 2. Put it where the API will serve it ----------------------------------------------------
Write-Step "Copying the build into the API's wwwroot"

# Emptied first: a stale index.html from an older build would name chunk files that are gone,
# and the site would load to a blank page.
if (Test-Path $WebRoot) {
    Remove-Item -Recurse -Force $WebRoot
}
New-Item -ItemType Directory -Force -Path $WebRoot | Out-Null
Copy-Item -Recurse -Force -Path (Join-Path $Dist "*") -Destination $WebRoot

# --- 3. Build the API -------------------------------------------------------------------------
Write-Step "Building the API ($Configuration)"
dotnet build $ApiProj -c $Configuration --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "The API build failed." }

# --- 4. ngrok, if asked -----------------------------------------------------------------------
$PublicUrl = $null
$NgrokProcess = $null

if ($Ngrok) {
    Write-Step "Starting the ngrok tunnel"

    if (-not (Get-Command ngrok -ErrorAction SilentlyContinue)) {
        throw "ngrok is not on PATH. Install it from https://ngrok.com/download, then run 'ngrok config add-authtoken <token>' once."
    }

    # Started detached with its own API on 4040, which is where the public URL is read from.
    $NgrokProcess = Start-Process -FilePath "ngrok" `
        -ArgumentList "http", "$Port", "--log=stdout", "--log-level=warn" `
        -PassThru -WindowStyle Hidden

    # The tunnel takes a moment to come up; poll rather than guess at a sleep.
    foreach ($attempt in 1..30) {
        Start-Sleep -Milliseconds 500
        try {
            $tunnels = Invoke-RestMethod -Uri "http://127.0.0.1:4040/api/tunnels" -TimeoutSec 2
            $PublicUrl = ($tunnels.tunnels | Where-Object { $_.proto -eq "https" } | Select-Object -First 1).public_url
            if ($PublicUrl) { break }
        }
        catch {
            # Not listening yet.
        }
    }

    if (-not $PublicUrl) {
        Write-Host "ngrok did not report a URL. Check 'ngrok http $Port' by hand." -ForegroundColor Yellow
    }
}

# --- 5. Run ------------------------------------------------------------------------------------
# Both stacks, not just 0.0.0.0. "localhost" resolves to ::1 first on Windows, so an IPv4-only
# bind makes every browser request from this machine wait for the IPv6 attempt to fail before
# retrying on IPv4: measured at a flat 205 ms per connection, which feels exactly like the lag
# this is meant to remove. [::] covers IPv6, 0.0.0.0 the LAN.
$Urls = "http://0.0.0.0:$Port;http://[::]:$Port"

Write-Step "Ghurify is starting"
Write-Host ""
Write-Host "  On this machine     http://localhost:$Port" -ForegroundColor Green
Write-Host "  On the Wi-Fi        http://${LanIp}:$Port" -ForegroundColor Green
if ($PublicUrl) {
    Write-Host "  Public (ngrok)      $PublicUrl" -ForegroundColor Green
}
Write-Host ""
Write-Host "  Health              http://${LanIp}:$Port/api/v1/health/ready"
Write-Host ""
Write-Host "If a phone cannot reach it, Windows Firewall is the usual reason. Allow the port once:" -ForegroundColor DarkGray
Write-Host "  New-NetFirewallRule -DisplayName 'Ghurify $Port' -Direction Inbound -LocalPort $Port -Protocol TCP -Action Allow" -ForegroundColor DarkGray
Write-Host ""
Write-Host "Ctrl+C to stop." -ForegroundColor DarkGray
Write-Host ""

# Settings the API cannot work out for itself. Passed as environment variables so nothing in the
# repository has to be edited, and so they are gone when this script exits.
$env:ASPNETCORE_ENVIRONMENT = $Environment
$env:ASPNETCORE_URLS        = $Urls
$env:Hosting__ServeWebApp   = "true"

# The payment gateway sends the traveller back to an absolute URL, and it must be one their
# browser can open: the tunnel if there is one, otherwise this machine's LAN address. Left alone
# when neither applies, so localhost development is unaffected.
$baseUrl = if ($PublicUrl) { $PublicUrl } else { "http://${LanIp}:$Port" }
$env:Payments__ApiBaseUrl = $baseUrl
$env:Payments__WebBaseUrl = $baseUrl

# One origin, so this is only a fallback for anything that still sends an Origin header.
$env:Cors__AllowedOrigins__0 = $baseUrl

try {
    # --no-launch-profile is required, not tidiness: launchSettings.json sets applicationUrl to
    # localhost, and with a launch profile active that wins over ASPNETCORE_URLS. The site would
    # then start, look fine on this machine, and refuse every device on the Wi-Fi.
    dotnet run --project $ApiProj -c $Configuration --no-build --no-launch-profile
}
finally {
    if ($NgrokProcess -and -not $NgrokProcess.HasExited) {
        Write-Host "Stopping ngrok." -ForegroundColor DarkGray
        Stop-Process -Id $NgrokProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
