<#
.SYNOPSIS
    Checks SMTP credentials before you wire them into the app.

.DESCRIPTION
    Authenticates against the mail server and sends one short message, then reports exactly
    what the server said. Use it to confirm a credential works without starting the API and
    walking the whole sign-in flow.

    It reads nothing from the project and writes nothing: pass everything as arguments, so a
    password never lands in a config file while you are still testing it.

    Uses System.Net.Mail rather than MailKit so it runs in Windows PowerShell 5.1, which
    cannot load the project's .NET 8 assemblies. Good enough to prove a credential; the API
    itself uses MailKit.

.EXAMPLE
    # Gmail, with a 16-character App Password (NOT your Google account password)
    ./scripts/test-email.ps1 -SmtpHost smtp.gmail.com -User you@gmail.com -Password "abcdefghijklmnop"

.EXAMPLE
    # Mailtrap sandbox: username and password are random strings from the Mailtrap inbox
    ./scripts/test-email.ps1 -SmtpHost sandbox.smtp.mailtrap.io -User 1a2b3c4d5e6f7g -Password "0987654321abcd" -To anyone@example.com
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $SmtpHost,
    [Parameter(Mandatory = $true)][string] $User,
    [Parameter(Mandatory = $true)][string] $Password,
    [string] $To,
    [int]    $Port = 587
)

$ErrorActionPreference = 'Stop'

if (-not $To) { $To = $User }

# Gmail and Mailtrap both expect a sender that matches the authenticated account.
$from = if ($User -like '*@*') { $User } else { 'no-reply@ghurify.test' }

Write-Host "Connecting to $SmtpHost`:$Port as $User ..." -ForegroundColor Cyan

$client = New-Object System.Net.Mail.SmtpClient($SmtpHost, $Port)
$client.EnableSsl = $true          # STARTTLS on 587
$client.Timeout = 20000
$client.DeliveryMethod = [System.Net.Mail.SmtpDeliveryMethod]::Network
$client.UseDefaultCredentials = $false
$client.Credentials = New-Object System.Net.NetworkCredential($User, $Password)

$message = New-Object System.Net.Mail.MailMessage
$message.From = New-Object System.Net.Mail.MailAddress($from, 'Ghurify')
$message.To.Add($To)
$message.Subject = 'Ghurify SMTP test'
$message.Body = 'If you are reading this, the credentials work. Set them with dotnet user-secrets.'

try {
    $client.Send($message)

    Write-Host "`nSMTP OK - a test message was sent to $To." -ForegroundColor Green
    Write-Host "Put it in .env at the repository root, then restart the API:" -ForegroundColor Green
    Write-Host "  Email__Host=$SmtpHost"
    Write-Host "  Email__UserName=$User"
    Write-Host "  Email__Password=<password>"
    Write-Host "  Email__FromAddress=$User"
    exit 0
}
catch {
    # SmtpException wraps the server's reply several layers down, and only the innermost
    # message says what actually went wrong, so walk the whole chain.
    $parts = @()
    $ex = $_.Exception
    while ($ex) {
        $parts += $ex.Message
        if ($ex -is [System.Net.Mail.SmtpException] -and $ex.StatusCode) {
            $parts += "SMTP status: $($ex.StatusCode)"
        }
        $ex = $ex.InnerException
    }
    $detail = ($parts | Select-Object -Unique) -join "`n  "

    Write-Host "`nSMTP FAILED" -ForegroundColor Red
    Write-Host "  $detail" -ForegroundColor Red

    # Gmail does not always return a readable reply through System.Net.Mail: a rejected login
    # often shows up only as the connection being closed, so treat that as a credential
    # problem too rather than sending the reader hunting for a network fault.
    if ($detail -match '5\.7\.8|BadCredentials|Username and Password not accepted|5\.7\.0|net_io_connectionclosed') {
        Write-Host "`nThe server rejected the username or password." -ForegroundColor Yellow
        Write-Host "Gmail has not accepted account passwords over SMTP since May 2022." -ForegroundColor Yellow
        Write-Host "Create a 16-character App Password instead:" -ForegroundColor Yellow
        Write-Host "  1. Turn on 2-Step Verification: https://myaccount.google.com/security"
        Write-Host "  2. Create one here:              https://myaccount.google.com/apppasswords"
        Write-Host "  3. Use those 16 characters as -Password (spaces can be left out)."
    }

    exit 1
}
finally {
    $message.Dispose()
    $client.Dispose()
}
