<#
.SYNOPSIS
    Seeds a demo database through the real Kaff ERP API endpoints - no raw SQL.

.DESCRIPTION
    Creates one Owner (POST /api/setup), then three staff users and ONE PORTAL CLIENT (POST /api/users)
    covering the
    landing kinds KAFF-125's shell actually renders: Owner and MarketingSales both get the honest
    "not built yet" surface (S-006, S-011), Hr gets the project-team landing, and Finance gets the
    profile/project-list landing.

    Then it registers TWO CLIENTS (KAFF-119, added 2026-09-04) and walks the duplicate-phone warning
    end to end: a corporate client on 01001234567, a phone-check on +20 100 123 4567 that finds it,
    a second client on the same number in Arabic-Indic digits refused with 409, and the same request
    acknowledged and accepted as C-10002. That last pair is the only part of this seed that shows a
    business rule deciding something rather than a form saving a row.

    See deploy/DEMO.md for the runbook this is part of, the resulting credentials, and - importantly -
    what this script still could NOT do (create a project) and why.

    Run only against an EMPTY database where GET /api/setup answers {"available":true}. It is not
    idempotent: the Owner step can run exactly once per database, by design (KAFF-100).

.PARAMETER Base
    The API's base URL. Defaults to http://localhost:5080, matching driver.mjs's own KAFF_API default.

    For STAGING pass the site root and nothing else - `-Base http://<staging-ip>`. nginx proxies
    /api/ to the API container, which is `expose`d and never published, so the site URL IS the API
    base there. See deploy/DEMO.md section 7.

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

function Post-Json([string]$Path, [string]$PayloadFile, [switch]$Authenticated, [hashtable]$Substitutions) {
    $file = Join-Path $payloadDir $PayloadFile

    if ($Substitutions -and $Substitutions.Count -gt 0) {
        # Read and re-encode through System.Text.Encoding.UTF8 EXPLICITLY, never through a PowerShell
        # string operator that would apply the ANSI codepage - the same trap .NOTES 1 documents for
        # Invoke-WebRequest. Only ASCII placeholders are substituted; the Arabic in these payloads is
        # never rebuilt, only round-tripped.
        $text = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)
        foreach ($key in $Substitutions.Keys) {
            $text = $text.Replace($key, [string]$Substitutions[$key])
        }
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($text)
    } else {
        $bytes = [System.IO.File]::ReadAllBytes($file)
    }

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

# 6. Client A - corporate, and the first client code this database ever issues (C-10001).
$r = Post-Json '/api/clients' 'payload-client-corporate.json' -Authenticated
Show 'POST /api/clients (corporate, phone typed as 01001234567)' $r
if ($r.StatusCode -ne 201) { throw 'Client registration failed - is this build older than KAFF-119 (86cc8b0)?' }
$corporateClientId = ($r.Body | ConvertFrom-Json).id

# 6b. The PORTAL user for that client - Role.Client, scoped to the client above (spec.md section 12).
#
#     V-33-E, 2026-09-05. Until now this script created NO Role.Client user at all, so the
#     client-portal boundary had no UI-level evidence anywhere in the repository: AC-126-L's E2E test
#     could drive Finance and could not drive the portal half, because the account did not exist.
#     It exists to be REFUSED - a Role.Client cannot hold a staff session
#     (StaffSessionRules.MayHoldStaffSession), so signing in with these correct credentials on the
#     staff host is turned away with the same generic refusal a wrong password gets (D-065). That
#     indistinguishability is the property, and it is now drivable.
$r = Post-Json '/api/users' 'payload-portal-client.json' -Authenticated -Substitutions @{ '__CLIENT_ID__' = $corporateClientId }
Show 'POST /api/users (Client portal: portal_client_demo / Demo#Portal1, scoped to the corporate client)' $r
if ($r.StatusCode -ne 201) { Write-Warning "Portal client user was not created ($($r.StatusCode)). spec.md section 12's boundary has no UI-level evidence without it - V-33-E." }

# 7. The same number in international form. AC-119-C: three formats, one match.
#    A 200 either way - the warning is not a refusal and never arrives as a Problem (D-107 section 2).
$r = Post-Json '/api/clients/phone-check' 'payload-client-phone-check.json' -Authenticated
Show 'POST /api/clients/phone-check (+20 100 123 4567 - expect ONE match, the corporate client above)' $r
if ($r.StatusCode -ne 200) { throw 'phone-check did not answer 200.' }
if ($r.Body -notmatch 'C-1') { Write-Warning 'phone-check found no match. Normalisation is not folding +20 100 123 4567 onto 01001234567 - the whole duplicate demo below is meaningless without it.' }

# 8. The company's owner, on the company's line - the same number a third time, in Arabic-Indic
#    digits, and NOT acknowledged. Expect 409: the save is held until somebody decides.
$r = Post-Json '/api/clients' 'payload-client-duplicate-unacknowledged.json' -Authenticated
Show 'POST /api/clients (individual, same number, acknowledged=false - expect 409)' $r
if ($r.StatusCode -ne 409) { Write-Warning "Expected 409 and got $($r.StatusCode). The duplicate check is not holding the save - AC-119-D." }

# 9. The same request with the acknowledgement. Expect 201 (C-10002) AND a
#    DuplicatePhoneAcknowledged row in the audit trail, whose subject is the MATCHED client.
#    AC-119-D and AC-119-E: the warning does not block the save, and the decision is in the trail.
$r = Post-Json '/api/clients' 'payload-client-duplicate-acknowledged.json' -Authenticated
Show 'POST /api/clients (same request, acknowledged=true - expect 201 and C-10002)' $r
if ($r.StatusCode -ne 201) { throw 'The acknowledged duplicate was still refused - AC-119-D is broken.' }

# 10. Project creation - expected to fail. Left in deliberately, not as a smoke check but as a live,
#    re-runnable demonstration of deploy/DEMO.md's central finding: no endpoint in this codebase
#    creates a project, so nothing above can be given a real project to be staffed on or to show.
Write-Output '== Probing POST /api/projects (expected: 404, no such endpoint exists today) =='
$r = Post-Json '/api/projects' 'payload-project-probe.json' -Authenticated
Show 'POST /api/projects' $r
if ($r.StatusCode -ne 404) {
    Write-Warning 'POST /api/projects did not 404 - a project-creation endpoint may have shipped since deploy/DEMO.md was written. Re-read it before relying on this script''s "no projects" framing.'
}

Write-Output 'Done. Credentials and what this seeds are recorded in deploy/DEMO.md.'
