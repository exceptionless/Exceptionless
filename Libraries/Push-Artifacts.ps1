$base_dir = Resolve-Path ".\"   
$artifactsDir = "$base_dir\artifacts"
$sourceDir = "$base_dir\Source"

if (!(Test-Path -Path $artifactsDir)) {
    Write-Host "Cloning repository..."
    git clone $env:BUILD_REPO_URL $artifactsDir 2>&1 | %{ "$_" }
    
    If ($LastExitCode -ne 0) {
        Write-Error "An error occurred while cloning the repository."
        Return $LastExitCode
    }
    
    Push-Location $artifactsDir
} else { 
    Write-Host "Pulling latest changes..."
    Push-Location $artifactsDir
    git pull 2>&1 | %{ "$_" }
    
    If ($LastExitCode -ne 0) {
        Write-Error "An error occurred while pulling the latest changes."
        Return $LastExitCode
    }
}


Write-Host "Removing existing files."
git rm -r * -q 2>&1 | %{ "$_" }

Write-Host "Copying build artifacts."
Write-Host "$sourceDir\Api" "$artifactsDir" /XD "$sourceDir\Api\obj" "$sourceDir\Api\App_Data" /S /XF "*.nuspec" "*.settings" "*.cs" "packages.config" "*.csproj" "*.user" "*.suo" "*.xsd" "*.ide" > log:nul
ROBOCOPY "$sourceDir\Api" "$artifactsDir" /XD "$sourceDir\Api\obj" "$sourceDir\Api\App_Data" /S /XF "*.nuspec" "*.settings" "*.cs" "packages.config" "*.csproj" "*.user" "*.suo" "*.xsd" "*.ide" > log:nul
ROBOCOPY "$artifactsDir\bin" "$artifactsDir\App_Data\JobRunner\bin\" /S > log:nul
Copy-Item -Path "$base_dir\packages\Foundatio.*\tools\Job.exe" -Destination "$artifactsDir\App_Data\JobRunner\" 
Copy-Item -Path "$base_dir\packages\Foundatio.*\tools\Job.pdb" -Destination "$artifactsDir\App_Data\JobRunner\" 
Copy-Item -Path "$sourceDir\WebJobs\App.config" -Destination "$artifactsDir\App_Data\JobRunner\Job.exe.config" 
Copy-Item -Path "$sourceDir\WebJobs\Job.bat" -Destination "$artifactsDir\App_Data\JobRunner\"
Copy-Item -Path "$sourceDir\WebJobs\NLog.config" -Destination "$artifactsDir\App_Data\JobRunner\"
Copy-Item -Path "$sourceDir\WebJobs\NLog.config" -Destination "$artifactsDir\App_Data\JobRunner\bin\"
ROBOCOPY "$sourceDir\Migrations\EventMigration\bin\Release" "$artifactsDir\App_Data\JobRunner\bin\" "Exceptionless.EventMigration*.*" > log:nul
ROBOCOPY "$sourceDir\WebJobs\continuous" "$artifactsDir\App_Data\jobs\continuous" /S > log:nul
ROBOCOPY "$sourceDir\WebJobs\triggered" "$artifactsDir\App_Data\jobs\triggered" /S > log:nul

Write-Host "Committing the latest changes...."
git add * 2>&1 | %{ "$_" }
git commit -a -m "Build $env:APPVEYOR_BUILD_VERSION" 2>&1 | %{ "$_" }
git push origin master 2>&1 | %{ "$_" }

If ($LastExitCode -ne 0) {
    Write-Error "An error occurred while committing the latest changes."
    Return $LastExitCode
} Else {
    Write-Host "Finished committing the latest changes."
}

Pop-Location