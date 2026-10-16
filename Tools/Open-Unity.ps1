param([string]$UnityPath)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
if (-not $UnityPath) {
    $editorRoot = Join-Path ${env:ProgramFiles} 'Unity\Hub\Editor'
    if (Test-Path -LiteralPath $editorRoot) {
        $UnityPath = Get-ChildItem -LiteralPath $editorRoot -Directory |
            Where-Object { $_.Name -like '6000.3.*' } |
            Sort-Object { [version]($_.Name -replace 'f\d+$', '') } -Descending |
            ForEach-Object { Join-Path $_.FullName 'Editor\Unity.exe' } |
            Where-Object { Test-Path -LiteralPath $_ } |
            Select-Object -First 1
    }
}
if ($UnityPath -and (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    Start-Process -FilePath $UnityPath -ArgumentList @('-projectPath', ('"' + $projectPath + '"')) -WindowStyle Hidden
    Write-Host 'Proyecto enviado a Unity. La primera importación crea la escena de la granja.'
    exit 0
}
Write-Host 'No se encontró Unity 6.3 LTS.'
Write-Host '1. Instala Unity Hub desde https://unity.com/download'
Write-Host '2. En Hub, instala el último parche disponible de Unity 6.3 LTS.'
Write-Host '3. Projects > Add > Add project from disk y selecciona:'
Write-Host $projectPath
Write-Host 'Para un editor en otra ruta: .\Tools\Open-Unity.ps1 -UnityPath "D:\Unity\Editor\Unity.exe"'
exit 1
