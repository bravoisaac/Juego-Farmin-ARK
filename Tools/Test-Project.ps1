param([string]$UnityPath)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$validationPath = Join-Path $projectPath 'Temp\Validation'
New-Item -ItemType Directory -Path $validationPath -Force | Out-Null
$manifest = Get-Content -LiteralPath (Join-Path $projectPath 'Packages\manifest.json') -Raw | ConvertFrom-Json
if ($manifest.dependencies.'com.unity.ugui' -ne '2.0.0') { throw 'Dependencia uGUI inesperada.' }
$compilerPath = $null
$vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (Test-Path -LiteralPath $vswherePath) {
    $vsPath = & $vswherePath -latest -products '*' -property installationPath
    if ($vsPath) { $compilerPath = Join-Path $vsPath 'MSBuild\Current\Bin\Roslyn\csc.exe' }
}
if (-not $compilerPath -or -not (Test-Path -LiteralPath $compilerPath)) {
    $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compilerPath)) { throw 'No hay compilador C#. Instala Visual Studio con Desarrollo de juegos con Unity.' }
$testExecutable = Join-Path $validationPath 'FarmRulesTests.exe'
& $compilerPath /nologo /target:exe ("/out:" + $testExecutable) (Join-Path $projectPath 'Assets\FarmGame\Scripts\Data\FarmState.cs') (Join-Path $projectPath 'Tools\FarmRulesTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación del modelo.' }
& $testExecutable
if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas del modelo.' }
if ($UnityPath) {
    if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) { throw 'UnityPath no existe.' }
    $logPath = Join-Path $validationPath 'UnityValidation.log'
    $arguments = @('-batchmode', '-quit', '-projectPath', ('"' + $projectPath + '"'), '-executeMethod', 'CozyFarm.Editor.FarmProjectSetup.ValidateProject', '-logFile', ('"' + $logPath + '"'))
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -PassThru -Wait -WindowStyle Hidden
    if ($process.ExitCode -ne 0) { throw "Unity falló. Revisa $logPath" }
    if (-not (Select-String -LiteralPath $logPath -Pattern 'VALIDATION PASSED' -Quiet)) { throw "Unity no confirmó validación. Revisa $logPath" }
    Write-Host 'Escena y shader validados en Unity.'
} else {
    Write-Host 'Modelo validado. Unity no se ejecutó; usa -UnityPath para validar escena y shader.'
}
