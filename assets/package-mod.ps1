param(
    [Parameter(Mandatory)][ValidateSet('0.4.6', '0.4.7')][string]$GameVersion,
    [Parameter(Mandatory)][string]$MonoGamePath,
    [Parameter(Mandatory)][string]$Il2CppGamePath,
    [string]$OutputRoot = (Join-Path $PSScriptRoot '../bin/packages')
)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
[xml]$project = Get-Content (Join-Path $projectRoot 'S1FuelMod.csproj') -Raw
$version = @($project.Project.PropertyGroup.Version | Where-Object { $_ })[0]
$constant = Get-Content (Join-Path $projectRoot 'Utils/Constants.cs') -Raw
if ($constant -notmatch ('MOD_VERSION = "' + [regex]::Escape($version) + '"')) { throw 'Project and MelonInfo versions must match.' }
$packageRoot = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
$records = @()
foreach ($runtime in @('Mono', 'IL2CPP')) {
    $gamePath = if ($runtime -eq 'Mono') { $MonoGamePath } else { $Il2CppGamePath }
    $gamePath = (Resolve-Path -LiteralPath $gamePath).Path
    $reference = if ($runtime -eq 'Mono') { 'Schedule I_Data/Managed/Assembly-CSharp.dll' } else { 'MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll' }
    if (-not (Test-Path (Join-Path $gamePath $reference))) { throw "Missing $runtime game references: $gamePath" }
    $buildRoot = Join-Path $projectRoot "bin/package-build/$GameVersion/$runtime"
    $archive = Join-Path $packageRoot "S1FuelMod-$version-ScheduleI-$GameVersion-$runtime.zip"
    if (Test-Path -LiteralPath $archive) { throw "Package already exists: $archive" }
    New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
    $buildLog = Join-Path $buildRoot 'build.log'
    & dotnet build (Join-Path $projectRoot 'S1FuelMod.csproj') -c "Release $runtime" -t:Rebuild --nologo -v:quiet `
        '-p:RunPostBuildEvent=Never' "-p:S1MonoDir=$MonoGamePath" "-p:S1CPPDir=$Il2CppGamePath" `
        -o $buildRoot *> $buildLog
    if ($LASTEXITCODE -ne 0) { Get-Content $buildLog -Tail 25; throw "$runtime build failed; see $buildLog" }
    $dll = Join-Path $buildRoot "S1FuelMod-$runtime.dll"
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString()
    if ($assemblyVersion -ne "$version.0") { throw "Unexpected assembly version: $assemblyVersion" }
    # Explicit allowlist: never package copied game or loader dependencies.
    Compress-Archive -LiteralPath $dll -DestinationPath $archive
    $records += [ordered]@{
        file = [IO.Path]::GetFileName($archive)
        sha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
        dllSha256 = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
        version = $version
        gameVersion = $GameVersion
        runtime = $runtime
        referenceSha256 = (Get-FileHash -LiteralPath (Join-Path $gamePath $reference) -Algorithm SHA256).Hash
    }
    Write-Host "Created $archive"
}
$records | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $packageRoot "S1FuelMod-$version-ScheduleI-$GameVersion-checksums.json")
