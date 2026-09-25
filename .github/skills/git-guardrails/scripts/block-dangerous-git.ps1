param(
    [string]$Command,
    [ValidateSet("Command", "PrePush")]
    [string]$Mode = "Command"
)

$ErrorActionPreference = "Stop"

$dangerousPatterns = @(
    @{ Pattern = "\bgit\s+push\b.*\s--force(?:-with-lease)?\b"; Reason = "force push can overwrite shared remote history" },
    @{ Pattern = "\bgit\s+reset\s+--hard\b"; Reason = "reset --hard discards local changes" },
    @{ Pattern = "\bgit\s+clean\s+-(?:[a-zA-Z]*f[a-zA-Z]*d|[a-zA-Z]*d[a-zA-Z]*f)\b"; Reason = "clean -fd deletes untracked files recursively" },
    @{ Pattern = "\bgit\s+checkout\s+(?:--\s+)?\.\s*$"; Reason = "checkout . discards worktree changes" },
    @{ Pattern = "\bgit\s+restore\s+(?:--worktree\s+)?\.\s*$"; Reason = "restore . discards worktree changes" },
    @{ Pattern = "\bgit\s+branch\s+-D\b"; Reason = "branch -D deletes branches without merge safety" },
    @{ Pattern = "\bgit\s+filter-(?:branch|repo)\b"; Reason = "filtering rewrites repository history" }
)

function Test-DangerousCommand {
    param([Parameter(Mandatory)][string]$Text)

    foreach ($entry in $dangerousPatterns) {
        if ($Text -match $entry.Pattern) {
            [Console]::Error.WriteLine("BLOCKED: '$Text' matches a dangerous git pattern: $($entry.Reason). Ask the user for explicit approval and use the Decision Gate first.")
            exit 2
        }
    }
}

if ($Mode -eq "PrePush") {
    $protectedBranches = @("main", "master", "develop", "release")
    $branch = (git rev-parse --abbrev-ref HEAD 2>$null).Trim()
    if ($LASTEXITCODE -ne 0) {
        [Console]::Error.WriteLine("Unable to determine current branch; refusing push until checked manually.")
        exit 2
    }

    if ($protectedBranches -contains $branch) {
        [Console]::Error.WriteLine("BLOCKED: direct push from protected branch '$branch'. Create a feature branch and pull request instead.")
        exit 2
    }

    exit 0
}

if ([string]::IsNullOrWhiteSpace($Command)) {
    $stdin = [Console]::In.ReadToEnd()
    if (-not [string]::IsNullOrWhiteSpace($stdin)) {
        $Command = $stdin.Trim()
    }
}

if ([string]::IsNullOrWhiteSpace($Command)) {
    Write-Output "No command supplied; nothing to block."
    exit 0
}

Test-DangerousCommand -Text $Command
Write-Output "OK: command did not match configured dangerous git patterns."
exit 0
