param([switch]$DesktopTests)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
Push-Location $PSScriptRoot
try {
    & $compiler /nologo /target:exe /out:CoreTests.exe /r:System.Drawing.dll Core.cs Tests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed' }
    & ./CoreTests.exe
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed' }
    & $compiler /nologo /target:exe /out:SettingsTests.exe Preferences.cs SettingsTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Settings test build failed' }
    & ./SettingsTests.exe
    if ($LASTEXITCODE -ne 0) { throw 'Settings tests failed' }
    if (Test-Path Program.cs) {
        & $compiler /nologo /target:winexe /platform:x64 /out:FocusShade.exe /win32manifest:app.manifest /r:System.Windows.Forms.dll /r:System.Drawing.dll Core.cs Native.cs WindowLayers.cs ButtonRenderer.cs Preferences.cs SettingsDialog.cs Program.cs
        if ($LASTEXITCODE -ne 0) { throw 'Application build failed' }
    }
    if ($DesktopTests) {
        & $compiler /nologo /target:exe /platform:x64 /main:DesktopTests /out:DesktopTests.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll Core.cs Native.cs WindowLayers.cs ButtonRenderer.cs Preferences.cs SettingsDialog.cs Program.cs DesktopTests.cs
        if ($LASTEXITCODE -ne 0) { throw 'Desktop test build failed' }
        & ./DesktopTests.exe
        if ($LASTEXITCODE -ne 0) { throw 'Desktop tests failed' }
    }
} finally { Pop-Location }
