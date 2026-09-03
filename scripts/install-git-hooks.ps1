$ErrorActionPreference = 'Stop'

$repositoryRoot = (git rev-parse --show-toplevel).Trim()
$hookSourceDirectory = Join-Path $repositoryRoot '.githooks'
git config --unset-all core.hooksPath 2>$null
$hookDirectory = (git rev-parse --git-path hooks).Trim()
$hookDestination = Join-Path $repositoryRoot $hookDirectory

if (-not (Test-Path -LiteralPath $hookSourceDirectory)) {
    throw "Hook source directory was not found: $hookSourceDirectory"
}

New-Item -ItemType Directory -Force -Path $hookDestination | Out-Null
foreach ($hookName in @('pre-commit', 'commit-msg')) {
    $hookSource = Join-Path $hookSourceDirectory $hookName
    if (-not (Test-Path -LiteralPath $hookSource)) {
        throw "Hook source was not found: $hookSource"
    }

    Copy-Item -LiteralPath $hookSource -Destination (Join-Path $hookDestination $hookName) -Force
}

Write-Output 'Git branch guard hook installed.'
