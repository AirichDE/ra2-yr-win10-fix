# Builds dist\RA2_Win10_Fix.exe with the C# compiler that ships with Windows (.NET Framework 4).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc  = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }
New-Item -ItemType Directory -Force (Join-Path $root 'dist') | Out-Null
& $csc /nologo /platform:x86 /target:exe /optimize+ `
    /out:"$root\dist\RA2_Win10_Fix.exe" `
    /r:System.Windows.Forms.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll `
    "$root\src\RA2Fix.cs"
if ($LASTEXITCODE -ne 0) { throw "csc failed ($LASTEXITCODE)" }
Get-FileHash "$root\dist\RA2_Win10_Fix.exe" -Algorithm SHA256 | Format-List Hash, Path
