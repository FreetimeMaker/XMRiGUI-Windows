param([string]$Executable = (Join-Path $PSScriptRoot '..\bin\Debug\net8.0-windows\win-x64\XMRiGUI.exe'))

$ErrorActionPreference = 'Stop'
$Executable = (Resolve-Path $Executable).Path
if (Get-Process XMRiGUI -ErrorAction SilentlyContinue) {
    throw 'Close XMRiGUI before running this test.'
}

$primary = $null
$secondary = $null

function Wait-Visibility([bool]$Visible) {
    for ($i = 0; $i -lt 80; $i++) {
        $primary.Refresh()
        if ($primary.HasExited) { throw "Primary exited with code $($primary.ExitCode)." }
        $current = Get-Process -Id $primary.Id
        if (($current.MainWindowHandle -ne [IntPtr]::Zero) -eq $Visible) { return }
        Start-Sleep -Milliseconds 100
    }
    throw "Expected primary window visibility: $Visible (session $($primary.SessionId), interactive: $([Environment]::UserInteractive))."
}

try {

    $primary = Start-Process $Executable -PassThru

    Wait-Visibility $true
    Write-Output 'Primary window opened.'
    $secondary = Start-Process $Executable -ArgumentList '--close' -PassThru
    if (!$secondary.WaitForExit(5000) -or $secondary.ExitCode -ne 0) { throw 'Second launch did not exit successfully.' }
    Wait-Visibility $false
    Write-Output 'Primary window hidden by second launch.'
    $secondary = Start-Process $Executable -ArgumentList '--open' -PassThru
    if (!$secondary.WaitForExit(5000) -or $secondary.ExitCode -ne 0) { throw 'Reopen launch did not exit successfully.' }
    Wait-Visibility $true
    Write-Output 'WPF startup and single-instance hide/reopen passed (no miner started).'
}
finally {
    foreach ($process in @($secondary, $primary)) {
        if ($null -ne $process -and !$process.HasExited) { $process.Kill(); [void]$process.WaitForExit(2500) }
    }

}
