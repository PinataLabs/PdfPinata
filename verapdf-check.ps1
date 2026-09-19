<#
.SYNOPSIS
    Builds the conformance corpus and asks veraPDF whether it conforms to what it claims.

.DESCRIPTION
    Every PDF/A and PDF/UA claim this library makes is otherwise self-certified: the writer refuses
    to save a document that breaks a rule it can check, and `PdfUaValidator` says in its own remarks
    which rules those are and which it cannot reach. This is the outside opinion. It runs the same
    way on a developer's machine and on a CI runner, which is the point of it being one script.

    veraPDF runs in Docker rather than being installed. It is a Java application, and requiring a JRE
    on every machine that wants to run this - and on Windows, where a developer is least likely to
    have one - costs more than a container does. The image is pinned so that a validator release
    cannot change the answer underneath a build; raise it deliberately.

    This gates. It reported without failing for exactly as long as the corpus did not conform, which
    was the three defects the first run found - all since fixed, all with tests of their own.

    Flavour detection is left automatic, which is why every document in the corpus makes a claim.
    veraPDF reads the claim out of each file's own metadata and holds it to that profile. A file
    claiming nothing would be held to the fallback flavour instead and fail for saying nothing rather
    than for being wrong.

.PARAMETER Corpus
    Where to write the corpus and validate from. Defaults to artifacts/conformance-corpus.

.PARAMETER NoGate
    Report the verdict without failing. The corpus conforms, so a failure means something regressed
    and the default is to say so with an exit code; this is for looking at a failure rather than
    being stopped by it.

.PARAMETER SkipBuild
    Validate the corpus already on disk instead of rebuilding it. For iterating on a validation
    failure without paying for a rebuild each time.

.PARAMETER Image
    The veraPDF image to run. Pinned by default.

.EXAMPLE
    ./verapdf-check.ps1
    Build the corpus, validate it, print a summary, and fail if anything does not conform.

.EXAMPLE
    ./verapdf-check.ps1 -NoGate
    The same, but always succeed - for reading a failure rather than being stopped by it.
#>
[CmdletBinding()]
param(
    [string] $Corpus = (Join-Path 'artifacts' 'conformance-corpus'),
    [switch] $NoGate,
    [switch] $SkipBuild,
    [string] $Image = 'verapdf/cli:v1.30.2'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Relative to the repository rather than to wherever this was invoked from, so that the corpus lands
# in the same place whichever directory a developer happens to be standing in.
$repository = $PSScriptRoot
if (-not [System.IO.Path]::IsPathRooted($Corpus)) {
    $Corpus = Join-Path $repository $Corpus
}

$reports = Join-Path $repository 'artifacts/verapdf-reports'

function Test-Docker {
    # `docker version` rather than `--version`: the latter answers from the client alone and says
    # nothing about whether a daemon is running, which is the failure a developer actually hits.
    $null = & docker version 2>&1
    return $LASTEXITCODE -eq 0
}

if (-not (Test-Docker)) {
    Write-Host ''
    Write-Host 'veraPDF runs in Docker, and Docker is not available here.' -ForegroundColor Yellow
    Write-Host ''
    Write-Host '  Windows/macOS: install Docker Desktop - https://docs.docker.com/get-docker/'
    Write-Host '  Linux:         sudo apt-get install -y docker.io, then add yourself to the'
    Write-Host '                 docker group and log in again.'
    Write-Host ''
    Write-Host 'Nothing was validated.' -ForegroundColor Yellow

    # A developer who has not installed Docker has not broken anything, and telling them so by
    # failing their build teaches the wrong lesson. On a runner it is the opposite: a validation step
    # that quietly validates nothing is how a claim goes back to being self-certified without anyone
    # deciding that it should, so there it is an error. CI is set by GitHub Actions and by every
    # other runner worth the name.
    if ($env:CI) {
        Write-Host 'This is CI, where a validator that cannot run is a failure.' -ForegroundColor Red
        exit 1
    }

    exit 0
}

if (-not $SkipBuild) {
    Write-Host 'Building the conformance corpus...' -ForegroundColor Cyan
    $project = Join-Path $repository 'src/ConformanceCorpus/ConformanceCorpus.csproj'
    # Out-Host rather than letting it fall into the pipeline: Write-Host below goes straight to the
    # console and this would not, so the corpus listing would arrive after the summary it precedes.
    & dotnet run --project $project -c Release -- --out $Corpus | Out-Host
    if ($LASTEXITCODE -ne 0) {
        # Worth distinguishing from a validation failure. The writer refuses to save a document that
        # breaks a rule of the profile it claims, so a corpus that will not build is this library's
        # own check firing - a real finding, and a different one.
        Write-Host 'The corpus could not be built, so there is nothing to validate.' -ForegroundColor Red
        exit 1
    }
}

$documents = @(Get-ChildItem -Path $Corpus -Filter '*.pdf' -File | Sort-Object Name)
if ($documents.Count -eq 0) {
    Write-Host "No PDFs in $Corpus - nothing to validate." -ForegroundColor Red
    exit 1
}

New-Item -ItemType Directory -Force -Path $reports | Out-Null

Write-Host ''
Write-Host "Validating $($documents.Count) document(s) with $Image" -ForegroundColor Cyan
Write-Host ''

$failed = @()

foreach ($document in $documents) {
    # One container run per document, rather than one recursive run over the directory. It costs a
    # few seconds and buys a report per document: a single combined report names the file in a
    # context path and is far harder to read when several fail for different reasons.
    $name = $document.BaseName

    $summary = & docker run --rm -v "$($Corpus):/data" $Image --format text "/data/$($document.Name)" 2>$null
    $conforms = $LASTEXITCODE -eq 0

    $detail = & docker run --rm -v "$($Corpus):/data" $Image --format xml "/data/$($document.Name)" 2>$null
    Set-Content -Path (Join-Path $reports "$name.xml") -Value $detail -Encoding UTF8

    if ($conforms) {
        Write-Host ("  PASS  {0}" -f $name) -ForegroundColor Green
        continue
    }

    $failed += $name

    # The flavour veraPDF picked, and the clauses it failed, read back out of the report it just
    # wrote. The text output names the rules; the XML says what each of them is about.
    $flavour = ($summary | Select-String -Pattern '^\s*FAIL\s+\S+\s+(\S+)\s*$').Matches.Groups[1].Value
    Write-Host ("  FAIL  {0}  ({1})" -f $name, $flavour) -ForegroundColor Red

    $rules = [regex]::Matches(
        ($detail -join "`n"),
        '<rule[^>]*clause="([^"]+)"[^>]*testNumber="(\d+)"[^>]*failedChecks="(\d+)"')

    foreach ($rule in $rules) {
        Write-Host ("          {0}-{1}   {2} failed check(s)" -f `
            $rule.Groups[1].Value, $rule.Groups[2].Value, $rule.Groups[3].Value)
    }
}

Write-Host ''
Write-Host ("{0} of {1} document(s) conform." -f ($documents.Count - $failed.Count), $documents.Count)
Write-Host "Full reports: $reports"

if ($failed.Count -eq 0) {
    exit 0
}

Write-Host ''
if ($NoGate) {
    Write-Host 'Not failing, because -NoGate was given.' -ForegroundColor Yellow
    exit 0
}

Write-Host 'The corpus conformed when this was written, so a failure here is a regression.' -ForegroundColor Red
Write-Host 'See docs/specs/verapdf-validation.md for what each clause is about.' -ForegroundColor Red
exit 1
