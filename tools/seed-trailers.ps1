# One-shot. Inserts the canonical `<!-- kaff ... -->` trailer into each slice-1 story file,
# immediately after its H1. Re-runnable: an existing trailer is replaced, not duplicated.
# After this runs, tools/status.ps1 is the only thing that reads story state.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# id | points | state | verdict | at | on
# state:   NOT-BUILT READY COMMITTED BUILT VERIFIED ACCEPTED FOLDED SUPERSEDED
# verdict: none PASS CONDITIONAL LAPSED REJECTED
$rows = @'
KAFF-100  | 5 | VERIFIED   | PASS        | 559ac45 | 2026-08-26
KAFF-101a | 5 | VERIFIED   | PASS        | 559ac45 | 2026-08-27
KAFF-101b | 3 | BUILT      | none        | f2b995b | 2026-09-02
KAFF-102  | 2 | VERIFIED   | PASS        | 559ac45 | 2026-08-27
KAFF-103  | 5 | VERIFIED   | PASS        | 559ac45 | 2026-08-27
KAFF-104  | 5 | READY      | none        | -       | 2026-08-22
KAFF-105a | 2 | VERIFIED   | PASS        | 559ac45 | 2026-08-27
KAFF-105b | 5 | VERIFIED   | PASS        | -       | 2026-08-30
KAFF-106  | 5 | VERIFIED   | CONDITIONAL | -       | 2026-08-25
KAFF-107  | 2 | FOLDED     | none        | -       | 2026-08-22
KAFF-108  | 3 | VERIFIED   | PASS        | -       | 2026-08-26
KAFF-109  | 5 | VERIFIED   | CONDITIONAL | 559ac45 | 2026-08-27
KAFF-110  | 5 | VERIFIED   | CONDITIONAL | -       | 2026-08-25
KAFF-111  | 3 | VERIFIED   | PASS        | -       | 2026-08-26
KAFF-112  | 3 | VERIFIED   | PASS        | -       | 2026-08-26
KAFF-113  | 5 | VERIFIED   | PASS        | -       | 2026-08-26
KAFF-114  | 3 | VERIFIED   | PASS        | -       | 2026-08-26
KAFF-115  | 8 | READY      | none        | -       | 2026-09-02
KAFF-116  | 3 | VERIFIED   | PASS        | -       | 2026-08-26
KAFF-117  | 5 | VERIFIED   | PASS        | 5b13761 | 2026-09-06
KAFF-118  | 3 | BUILT      | none        | -       | 2026-09-05
KAFF-119  | 5 | VERIFIED   | PASS        | 86cc8b0 | 2026-09-04
KAFF-120  | 2 | VERIFIED   | PASS        | -       | 2026-09-04
KAFF-121  | 3 | VERIFIED   | PASS        | -       | 2026-09-04
KAFF-122  | 0 | SUPERSEDED | none        | -       | 2026-08-21
KAFF-123  | 2 | VERIFIED   | PASS        | -       | 2026-09-04
KAFF-124  | 2 | VERIFIED   | PASS        | -       | 2026-09-04
KAFF-125  | 3 | VERIFIED   | LAPSED      | 8ea9258 | 2026-09-06
KAFF-126  | 8 | VERIFIED   | PASS        | -       | 2026-09-04
KAFF-127  | 8 | VERIFIED   | CONDITIONAL | 116c08a | 2026-09-06
KAFF-128  | 3 | READY      | none        | -       | 2026-09-05
'@ -split "`n" | Where-Object { $_.Trim() }

foreach ($row in $rows) {
    $f = $row -split '\|' | ForEach-Object { $_.Trim() }
    $id, $pts, $state, $verdict, $at, $on = $f
    $file = Get-ChildItem "$root\stories" -Recurse -Filter "$id-*.md" | Select-Object -First 1
    if (-not $file) { Write-Warning "no file for $id"; continue }

    $trailer = "<!-- kaff id=$id slice=1 points=$pts state=$state verdict=$verdict at=$at on=$on -->"
    $lines = [System.Collections.Generic.List[string]](Get-Content $file.FullName -Encoding UTF8)
    $lines.RemoveAll({ param($l) $l -like '<!-- kaff id=*' }) | Out-Null
    $h1 = $lines.FindIndex({ param($l) $l -like '# *' })
    if ($h1 -lt 0) { Write-Warning "no H1 in $($file.Name)"; continue }
    $lines.Insert($h1 + 1, '')
    $lines.Insert($h1 + 2, $trailer)
    [System.IO.File]::WriteAllText($file.FullName, ($lines -join "`n") + "`n", [System.Text.UTF8Encoding]::new($false))
    "seeded $id"
}
