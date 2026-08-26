# check-citations.ps1 - the enforceable half of SM-31 (process/agile.md; decisions.md D-058, D-059).
#
# A citation must name a STABLE IDENTIFIER, not a position:
#     [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectManage`]
#
# This script asserts the cited identifier actually appears in the cited file. That is the whole
# check, and it is a grep. It exists because the previous rule - "cite from a grep -n" - was a
# request to be careful, and D-058 measured what that was worth: ~68 citations into one file across
# 30 distinct line numbers, of which exactly one was still right.
#
# Why it does not validate line numbers: a line number ALWAYS resolves. A resolvability check
# reported 194/197 healthy on a corpus where dozens pointed at blank lines and comment fragments.
# A green light with no evidence behind it is worse than no check (D-046).
#
# This file is deliberately pure ASCII, and uses no backslash literals. PowerShell 5.1 reads a
# BOM-less UTF-8 file as cp1252 and mangles every non-ASCII byte - the first draft died on a
# Unicode arrow. See decisions.md D-056 section 5.
#
# Exit 0 = clean. Exit 1 = a cited identifier is absent, or a legacy line-number citation remains.

param([string]$Root = (Join-Path $PSScriptRoot '..'))

$Root = (Resolve-Path $Root).Path
$tick = [char]0x60
$sep  = [char]92

$binDir = $sep + 'bin' + $sep
$objDir = $sep + 'obj' + $sep
$modDir = $sep + 'node_modules' + $sep

function Test-Interesting($path) {
    return -not ($path.Contains($binDir) -or $path.Contains($objDir) -or $path.Contains($modDir))
}

# @ `File.cs` -> `Identifier`      (the "the" and a trailing " row" are allowed noise)
$identPattern  = '@\s*' + $tick + '([^' + $tick + ']+\.(?:cs|ts|html|sql|ps1|json|md))' + $tick +
                 '\s*->\s*(?:the\s*)?' + $tick + '([^' + $tick + ']+)' + $tick
# `File.cs:123` anywhere - the legacy form SM-31 retires, WITH OR WITHOUT a leading "@".
#
# The "@" prefix is deliberately not required. SM-31 originally permitted a bare line number as a
# "convenience hint" after the identifier; QA measured that exemption on 2026-08-22 and found 77 of
# them repo-wide, decaying at the same rate as the claims they were exempted from - PermissionCatalogue
# .cs:258 appeared 8 times and is a blank line. A checker blind to them reports green on a lying
# corpus, which is the exact failure D-058 was written about. The exemption is withdrawn: a line
# number is not a citation in any position. See decisions.md D-059 section 9.
#
# The closing backtick is required, not optional: matching now runs over a whole file's text (see
# below) so a wrap can be checked, and without a closing tick the path class - [^`]+, which allows
# newlines - keeps consuming everything until it happens to hit some unrelated ".ext:digits" text
# anywhere later in the document, bridging two unconnected paragraphs into one bogus "citation".
# Requiring the pair closed is what a real `` `File.cs:123` `` citation already looks like, and is
# what stops that. \s* around the colon and before the close tick, same as the identifier pattern's
# \s* around "->", is what lets a citation wrapped between the path and the line number - or between
# the line number and the closing tick - still be seen instead of silently split in half.
$legacyPattern = $tick + '([^' + $tick + ']+\.(?:cs|ts|html|sql|ps1|json|md))' + '\s*:\s*' + '[0-9]+' + '\s*' + $tick

$docs = Get-ChildItem -Path $Root -Recurse -Filter *.md -File |
        Where-Object { Test-Interesting $_.FullName }

$sources = @{}
foreach ($src in (Get-ChildItem -Path $Root -Recurse -File -Include *.cs, *.ts, *.html, *.sql, *.ps1, *.json, *.md |
                  Where-Object { Test-Interesting $_.FullName })) {
    if (-not $sources.ContainsKey($src.Name)) { $sources[$src.Name] = @() }
    $sources[$src.Name] += $src.FullName
}

$broken = @(); $legacy = @(); $checked = 0

foreach ($doc in $docs) {
    # Read the whole file as one string rather than line-by-line. A citation wrapped across two
    # lines - which is exactly what happens when a deep path and a long member name hit the repo's
    # 100-column wrap - has a newline where the old per-line match had only $line. \s* in both
    # patterns already matches a newline; the only change needed is to stop cutting the text into
    # lines before the regex ever sees it. Line numbers for reporting are recovered from the match
    # position afterward.
    $text = Get-Content -LiteralPath $doc.FullName -Encoding UTF8 -Raw
    if ([string]::IsNullOrEmpty($text)) { continue }

    foreach ($m in [regex]::Matches($text, $identPattern)) {
        $checked++
        $cited = $m.Groups[1].Value
        $leaf  = Split-Path $cited -Leaf
        $ident = ($m.Groups[2].Value -replace '\s+row$', '').Trim()
        $lineNo = ($text.Substring(0, $m.Index).Split("`n")).Count
        $where = "$($doc.Name):$lineNo"

        if (-not $sources.ContainsKey($leaf)) {
            $broken += "$where  file not found: $leaf"
        }
        else {
            # Two files in this repo can share a name - ux/questions.md and qa/questions.md do.
            # Keying only on the leaf checked every citation against whichever one the walk
            # happened to find first, and reported the other one broken while it was correct:
            # a false red, which teaches people to ignore the checker. That is D-046's disease
            # in the tool built to cure it. Narrow by the cited path where one is given; where
            # none is, accept the identifier in any file of that name.
            $tail = $cited.Replace('/', [string]$sep)
            $candidates = @($sources[$leaf] | Where-Object { $_.EndsWith($tail, 'OrdinalIgnoreCase') })
            if ($candidates.Count -eq 0) { $candidates = @($sources[$leaf]) }

            $found = $false
            foreach ($candidate in $candidates) {
                if (Select-String -LiteralPath $candidate -Pattern ([regex]::Escape($ident)) -Quiet) {
                    $found = $true
                    break
                }
            }

            if (-not $found) { $broken += "$where  '$ident' is absent from $cited" }
        }
    }

    foreach ($m in [regex]::Matches($text, $legacyPattern)) {
        $lineNo = ($text.Substring(0, $m.Index).Split("`n")).Count
        $legacy += "$($doc.Name):$lineNo  $($m.Groups[1].Value)"
    }
}

Write-Output ('SM-31 identifier citations checked: ' + $checked)
Write-Output ('  broken (identifier absent):        ' + $broken.Count)
Write-Output ('  legacy line-number citations:      ' + $legacy.Count)

if ($broken.Count) {
    Write-Output ''
    Write-Output 'BROKEN - a cited identifier does not exist:'
    $broken | ForEach-Object { Write-Output ('  ' + $_) }
}

if ($broken.Count -or $legacy.Count) { exit 1 }
exit 0
