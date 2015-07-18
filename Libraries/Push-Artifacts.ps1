$base_dir = Resolve-Path ".\"   
$artifactsDir = "$base_dir\artifacts"
$sourceDir = "$base_dir\Source"

If ($env:APPVEYOR_PULL_REQUEST_NUMBER -ne $null) {
    Write-Host "Artifacts will not be created for pull requests."
    Return
}

Write-Host "Cloning repository into $($artifactsDir)..."
git clone "$env:BUILD_REPO_URL" "$artifactsDir" -q 2>&1 | %{ "$_" }

If ($LastExitCode -ne 0) {
    Write-Error "An error occurred while cloning the repository."
    Return $LastExitCode
}

Push-Location $artifactsDir

git fetch --all -f -q 2>&1 | %{ "$_" }
$branches = (git branch -r) 2> $null
If (($branches.Replace(" ", "").Split([environment]::NewLine) -contains "origin/$($env:APPVEYOR_REPO_BRANCH)") -eq $True) {
    Write-Host "Checking out branch: $env:APPVEYOR_REPO_BRANCH"
    git checkout "$env:APPVEYOR_REPO_BRANCH" -f -q 2>&1 | %{ "$_" }
} else {
    Write-Host "Checking out new branch: $env:APPVEYOR_REPO_BRANCH"
    git checkout -b "$env:APPVEYOR_REPO_BRANCH" -q 2>&1 | %{ "$_" }
}

If ($LastExitCode -ne 0) {
    Write-Error "An error occurred while changing to branch: $env:APPVEYOR_REPO_BRANCH"
    Return $LastExitCode
}

Write-Host "Removing existing files..."
git rm -r * -q 2>&1 | %{ "$_" }

Write-Host "Copying build artifacts..."
ROBOCOPY "$sourceDir\Api" "$artifactsDir" /XD "$sourceDir\Api\obj" "$sourceDir\Api\App_Data" /S /XF "*.nuspec" "*.settings" "*.cs" "packages.config" "*.csproj" "*.user" "*.suo" "*.xsd" "*.ide" /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$artifactsDir\bin" "$artifactsDir\App_Data\JobRunner\bin\" /S /NFL /NDL /NJH /NJS /nc /ns /np
Copy-Item -Path "$base_dir\packages\Foundatio.*\tools\Job.exe" -Destination "$artifactsDir\App_Data\JobRunner\" 
Copy-Item -Path "$base_dir\packages\Foundatio.*\tools\Job.pdb" -Destination "$artifactsDir\App_Data\JobRunner\" 
Copy-Item -Path "$base_dir\packages\Foundatio.*\tools\bin\CommandLine.dll" -Destination "$artifactsDir\App_Data\JobRunner\bin\" 
Copy-Item -Path "$base_dir\packages\Foundatio.*\tools\bin\CommandLine.xml" -Destination "$artifactsDir\App_Data\JobRunner\bin\" 
Copy-Item -Path "$sourceDir\WebJobs\App.config" -Destination "$artifactsDir\App_Data\JobRunner\Job.exe.config" 
Copy-Item -Path "$sourceDir\WebJobs\Job.bat" -Destination "$artifactsDir\App_Data\JobRunner\"
Copy-Item -Path "$sourceDir\WebJobs\NLog.config" -Destination "$artifactsDir\App_Data\JobRunner\"
ROBOCOPY "$sourceDir\Migrations\EventMigration\bin\Release" "$artifactsDir\App_Data\JobRunner\bin\" "Exceptionless.EventMigration*.*" /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Migrations\EventMigration\bin\Release" "$artifactsDir\App_Data\JobRunner\bin\" "MongoDB*.*" /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\WebJobs\continuous" "$artifactsDir\App_Data\jobs\continuous" /S /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\WebJobs\triggered" "$artifactsDir\App_Data\jobs\triggered" /S /NFL /NDL /NJH /NJS /nc /ns /np

Write-Host "Committing the latest changes..."
git add * 2>&1 | %{ "$_" }
git commit -a -m "Build: $env:APPVEYOR_BUILD_VERSION $($env:APPVEYOR_REPO_NAME)@$($env:APPVEYOR_REPO_COMMIT)" -q 2>&1 | %{ "$_" }
git push origin "$env:APPVEYOR_REPO_BRANCH" -q 2>&1 | %{ "$_" }

If ($LastExitCode -ne 0) {
    Write-Error "An error occurred while committing the latest changes."
    Return $LastExitCode
} Else {
    Write-Host "Finished committing the latest changes."
}

Pop-Location