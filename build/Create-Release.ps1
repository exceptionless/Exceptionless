Function Clone-Repository([string] $repoUrl, [string] $directory) {
    if (!(Test-Path -Path $directory)) {
        Write-Host "Cloning repository..."
        git clone $repoUrl $directory 2>&1 | %{ "$_" }

        If ($LastExitCode -ne 0) {
            Write-Error "An error occurred while cloning the repository."
            Return $LastExitCode
        }
    } else {
        Write-Host "Pulling latest changes..."
        Push-Location $directory

        git pull 2>&1 | %{ "$_" }
        If ($LastExitCode -ne 0) {
            Write-Error "An error occurred while pulling the latest changes."
            Return $LastExitCode
        }

        Pop-Location
    }
}

If ($env:APPVEYOR_REPO_BRANCH -ne 'master' -or $env:APPVEYOR_REPO_TAG -ne $True) {
    Write-Host "The release will only be created from a tag on the master branch."
    Return
}

$base_dir = Resolve-Path ".\"
$releaseDir = "$base_dir\release"
$releaseArtifactsDir = "$releaseDir\artifacts"
$releaseTempDir = "$releaseDir\temp"

Clone-Repository $env:BUILD_APP_REPO_URL "$releaseArtifactsDir\app"
Clone-Repository $env:BUILD_REPO_URL "$releaseArtifactsDir\api"

Write-Host "Copying release artifacts"
If (Test-Path -Path $releaseTempDir) {
    Remove-Item -Recurse -Force $releaseTempDir | Out-Null
}

ROBOCOPY "$releaseArtifactsDir\api" "$releaseTempDir\wwwroot" /XD "$releaseArtifactsDir\api\.git" /XF "exceptionless.png" "favicon.ico" /S /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$releaseArtifactsDir\app" "$releaseTempDir\wwwroot" /XD "$releaseArtifactsDir\app\.git" /S /XF "web.config" /NFL /NDL /NJH /NJS /nc /ns /np
Copy-Item -Path "$base_dir\build\Start-ElasticSearch.ps1" -Destination $releaseTempDir
Copy-Item -Path "$base_dir\build\elasticsearch.yml" -Destination $releaseTempDir
Copy-Item -Path "$base_dir\build\elasticsearch.prod.yml" -Destination $releaseTempDir
Copy-Item -Path "$base_dir\build\Start-Website.ps1" -Destination $releaseTempDir
"PowerShell .\Start-Elasticsearch.ps1`r`nPowerShell .\Start-Website.ps1" | Out-File "$releaseTempDir\Start.bat" -Encoding "ascii"
Copy-Item -Path "$base_dir\build\readme.txt" -Destination $releaseTempDir

Write-Host "Merging configuration"
$webConfig = "$releaseTempDir\wwwroot\web.config"
$apiConfig = [xml](Get-Content $webConfig)

# Copy settings from app web.config
$appConfig = [xml](Get-Content "$releaseArtifactsDir\app\web.config")
$apiConfig.SelectSingleNode("configuration").AppendChild($apiConfig.ImportNode($appConfig.SelectSingleNode("configuration/location"), $true)) | Out-Null
$apiConfig.SelectSingleNode("configuration/system.webServer").AppendChild($apiConfig.CreateComment($apiConfig.ImportNode($appConfig.SelectSingleNode("configuration/system.webServer/rewrite"), $true).OuterXml)) | Out-Null
$apiConfig.Save($webConfig)

$appProdSettings = "$releaseTempDir\wwwroot\appsettings.Production.yml"
$prodConfig = (Get-Content $appProdSettings)
$prodConfig = $prodConfig -Replace "BaseURL: 'https://be.exceptionless.io'", "BaseURL: 'http://localhost:50000/#!'"
$prodConfig = $prodConfig -Replace "EnableSSL: true", "EnableSSL: false"
$prodConfig = $prodConfig -Replace "RunJobsInProcess: false", "RunJobsInProcess: true"
Set-Content -Path $appProdSettings -Value $prodConfig

Write-Host "Zipping release"
If (Test-Path -Path "$releaseDir\Exceptionless.$($env:APPVEYOR_BUILD_VERSION).zip") {
    Remove-Item -Recurse -Force "$releaseDir\Exceptionless.$($env:APPVEYOR_BUILD_VERSION).zip" | Out-Null
}

Add-Type -assembly "system.io.compression.filesystem"
[io.compression.zipfile]::CreateFromDirectory($releaseTempDir, "$releaseDir\Exceptionless.$($env:APPVEYOR_BUILD_VERSION).zip")

 Get-ChildItem .\release\*.zip | % { Push-AppveyorArtifact $_.FullName -FileName $_.Name -DeploymentName ReleaseArtifacts }