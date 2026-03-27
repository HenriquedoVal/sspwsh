param([switch]$pack, [switch]$install)

$project_name = "NewShellServer"
$project_version = "0.0.1"
$csproj_platform = "net8.0-windows"
$script_to_process = "SetPromptText.ps1"

dotnet build --configuration Release "/p:VersionPrefix=$project_version"
if (-not $?) { exit 1 }

$dll = ".\bin\Release\$csproj_platform\$project_name.dll"
if (-not (Test-Path $dll)) {
    Write-Output "Could not find $project_name.dll"
    exit 1
}

$manifest = ".\$project_name.psd1"
if (-not (Test-Path $manifest)) {
    Write-Output "Could not find $project_name.psd1"
    exit 1
}


if (-not ($pack -or $install)) { exit 0 }


$pack_dirs = "$project_name\$project_version"  

mkdir $pack_dirs -ErrorAction Ignore > $null
copy $manifest $pack_dirs
copy $dll $pack_dirs
copy $script_to_process $pack_dirs

Update-ModuleManifest -Path "$pack_dirs\$project_name.psd1" -ModuleVersion $project_version -WhatIf


if (-not $install) { exit 0 }


$install_path = $env:PSModulePath.Split(';')[0]
copy $project_name $install_path -Recurse -Force
