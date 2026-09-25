# Human-in-the-loop reproduction loop for Windows PowerShell.
# Copy this file, edit the steps below, and run it from the repository root.
# The agent runs the script; the user follows prompts in their terminal.
#
# Usage:
#   pwsh .\scripts\hitl-loop.template.ps1
# or:
#   powershell -ExecutionPolicy Bypass -File .\scripts\hitl-loop.template.ps1
#
# Helpers:
#   Step "instruction"          shows instruction and waits for Enter
#   Capture "KEY" "question"   asks a question and stores the answer
#
# At the end, captured values are printed as KEY=VALUE for the agent to parse.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Captured = [ordered]@{}

function Step {
    param([Parameter(Mandatory = $true)][string]$Instruction)

    Write-Host ""
    Write-Host ">>> $Instruction"
    Read-Host "    Press Enter when done" | Out-Null
}

function Capture {
    param(
        [Parameter(Mandatory = $true)][string]$Key,
        [Parameter(Mandatory = $true)][string]$Question
    )

    Write-Host ""
    Write-Host ">>> $Question"
    $Captured[$Key] = Read-Host "    >"
}

# --- edit below ---------------------------------------------------------

Step "Open the Roamly app at http://localhost:5173 and sign in."

Capture "ERRORED" "Click the action that reproduces the bug. Did it throw an error? (y/n)"

Capture "ERROR_MSG" "Paste the error message, network status, or 'none':"

# --- edit above ---------------------------------------------------------

Write-Host ""
Write-Host "--- Captured ---"
foreach ($entry in $Captured.GetEnumerator()) {
    Write-Host "$($entry.Key)=$($entry.Value)"
}