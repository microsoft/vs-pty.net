if ($env:AGENT_TOOLSDIRECTORY) {
    $path = "$env:AGENT_TOOLSDIRECTORY\pty.net\tools"
} elseif ($env:localappdata) {
    $path = "$env:localappdata\pty.net\tools"
} else {
    $path = "$PSScriptRoot\..\obj\tools"
}

if (!(Test-Path $path)) {
    New-Item -ItemType Directory -Path $Path | Out-Null
}

(Resolve-Path $path).Path