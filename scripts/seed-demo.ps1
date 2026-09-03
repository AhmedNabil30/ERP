<#
.SYNOPSIS
    Seeds a demo database through the real Kaff ERP API endpoints - no raw SQL.

.DESCRIPTION
    Creates one Owner (POST /api/setup), then three staff users (POST /api/users) covering the
    landing kinds KAFF-125's shell actually renders: Owner and MarketingSales both get the honest
    "not built yet" surface (S-006, S-011), Hr gets the project-team landing, and Finance gets the
    profile/project-list landing. See deploy/DEMO.md for the runbook this is part of, the resulting
    credentials, and - importantly - what this script could NOT do (create a project) and why.

    Run only against an EMPTY database where GET /api/setup answers {"available":true}. It is not
    idempotent: the Owner step can run exactly once per database, by design (KAFF-100).

.PARAMETER Base
    The API's base URL. Defaults to http://localhost:5080, matching driver.mjs's own KAFF_API default.

.NOTES
    Two things learned the hard way while writing this, both explained in deploy/DEMO.md:

    1. PowerShell 5.1 mis-decodes non-ASCII text through Invoke-WebRequest's own charset guessing (the
       same class of bug SKILL.md documents for file editing) - this script therefore uses
       System.Net.Http.HttpClient directly, reads every payload as raw UTF-8 bytes from the
       payload-*.json files beside this script, and never lets a PowerShell string carry the Arabic
       text on its way through.
    2. The auth cookie is Secure (D-050), and .NET's CookieContainer refuses to attach a Secure cookie
       to a plain http:// request even to localhost - a real browser exempts localhost from that rule,
       a scripted HttpClient does not. The handler's automatic cookie handling is switched off
       (UseCookies = $false) and the Set-Cookie value is replayed by hand as a literal Cookie header on
       every authenticated call below.
#>
param(
    [string]$Base = 'http://localhost:5080'
)

$ErrorActionPreference = 'Stop'
$payloadDir = Join-Path $PSScriptRoot 'seed-demo'
Add-Type -AssemblyName System.Net.Http

$handler = New-Object System.Net.Http.HttpClientHandler
$handler.UseCookies = $false
$client = New-Object System.Net.Http.HttpClient($handler)
$script:cookie = $null

function Post-Json([string]$Path, [string]$PayloadFile, [switch]$Authenticated) {
    $bytes = [System.IO.File]::ReadAllBytes((Join-Path $payloadDir $PayloadFile))
    $content = New-Object System.Net.Http.ByteArrayContent(,$bytes)
    $content.Headers.ContentType = New-Object System.Net.Http.Headers.MediaTypeHeaderValue('application/json')

    $request = New-Object System.Net.Http.HttpRequestMessage('POST', "$Base$Path")
    $request.Content = $content
    if ($Authenticated -and $script:cookie) {
        $request.Headers.TryAddWithoutValidation('Cookie', $script:cookie) | Out-Null
    }

    $response = $client.SendAsync($request).GetAwaiter().GetResult()

    if ($response.Headers.Contains('Set-Cookie')) {
        $setCookie = $response.Headers.GetValues('Set-Cookie') | Select-Object -First 1
        $script:cookie = ($setCookie -split ';')[0]
    }

    $bodyBytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
    [PSCustomObject]@{
        StatusCode = [int]$response.StatusCode
        Body       = [System.Text.Encoding]::UTF8.GetString($bodyBytes)
    }
}

function Show([string]$Label, $Result) {
    Write-Output "== $Label =="
    Write-Output "status: $($Result.StatusCode)"
    if ($Result.Body) { Write-Output $Result.Body }
    Write-Output ''
}

Write-Output "Seeding against $Base ..."
Write-Output ''

# 1. Owner - POST /api/setup, anonymous, one-time only per database.
$r = Post-Json '/api/setup' 'payload-owner.json'
Show 'POST /api/setup (Owner: owner_demo / Demo#Owner1)' $r
if ($r.StatusCode -ne 201) { throw 'Owner bootstrap failed - is the database really empty? See deploy/DEMO.md.' }

# 2. Sign in as Owner - captures the auth cookie for every call below.
$r = Post-Json '/api/auth/sign-in' 'payload-signin.json'
Show 'POST /api/auth/sign-in (Owner)' $r
if ($r.StatusCode -ne 204) { throw 'Owner sign-in failed.' }

# 3. Hr user - lands on the project-team surface (D-051 Q32).
$r = Post-Json '/api/users' 'payload-hr.json' -Authenticated
Show 'POST /api/users (Hr: hend_hr_demo / Demo#Hr123, mustChangePassword)' $r

# 4. Finance user - the profile-only role KAFF-125's landing renders a project list for.
$r = Post-Json '/api/users' 'payload-finance.json' -Authenticated
Show 'POST /api/users (Finance: sara_finance_demo / Demo#Fin123, mustChangePassword)' $r

# 5. MarketingSales user - the other "not built yet" landing kind (S-011).
$r = Post-Json '/api/users' 'payload-marketing.json' -Authenticated
Show 'POST /api/users (MarketingSales: karim_sales_demo / Demo#Sales123, mustChangePassword)' $r

# 6. Project creation - expected to fail. Left in deliberately, not as a smoke check but as a live,
#    re-runnable demonstration of deploy/DEMO.md's central finding: no endpoint in this codebase
#    creates a project, so nothing above can be given a real project to be staffed on or to show.
Write-Output '== Probing POST /api/projects (expected: 404, no such endpoint exists today) =='
$r = Post-Json '/api/projects' 'payload-project-probe.json' -Authenticated
Show 'POST /api/projects' $r
if ($r.StatusCode -ne 404) {
    Write-Warning 'POST /api/projects did not 404 - a project-creation endpoint may have shipped since deploy/DEMO.md was written. Re-read it before relying on this script''s "no projects" framing.'
}

Write-Output 'Done. Credentials and what this seeds are recorded in deploy/DEMO.md.'
