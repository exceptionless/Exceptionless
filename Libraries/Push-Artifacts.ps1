Function Git-Pull() {
    Write-Host "Pulling latest changes..."
    Push-Location $artifactsDir
    git pull 2>&1 | %{ "$_" }
    
    If ($LastExitCode -ne 0) {
        Write-Error "An error occurred while pulling the latest changes."
        Return $LastExitCode
    }
}

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
}

Push-Location $artifactsDir
Git-Pull

$branch = "$env:APPVEYOR_REPO_BRANCH";
if ($env:APPVEYOR_PULL_REQUEST_NUMBER -ne $null) {
    $branch = "$($env:APPVEYOR_REPO_BRANCH)-$($env:APPVEYOR_PULL_REQUEST_NUMBER)"
    git fetch origin "ref/pull/$($env:APPVEYOR_PULL_REQUEST_NUMBER)/head:$($branch)" -q 2>&1 | %{ "$_" }
}

git fetch -q 2>&1 | %{ "$_" }
$branches = git branch 2> $null
if ("$branches" -match $branch) {
    Write-Host "Checking out branch: $branch"
    git checkout $branch -q 2>&1 | %{ "$_" }
    Git-Pull
} else {
    Write-Host "Checking out new branch: $branch"
    git checkout -b "$branch" -q 2>&1 | %{ "$_" }
}

If ($LastExitCode -ne 0) {
    Write-Error "An error occurred while changing to branch: $branch"
    Return $LastExitCode
}

Write-Host "Removing existing files."
git rm -r * -q 2>&1 | %{ "$_" }

Write-Host "Copying build artifacts."
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

Write-Host "Committing the latest changes...."
git add * 2>&1 | %{ "$_" }
git commit -a -m "Build: $env:APPVEYOR_BUILD_VERSION Author: $env:APPVEYOR_REPO_COMMIT_AUTHOR Commit: $($env:APPVEYOR_REPO_NAME)@$($env:APPVEYOR_REPO_COMMIT)" -q 2>&1 | %{ "$_" }
git push origin "$branch" -q 2>&1 | %{ "$_" }

If ($LastExitCode -ne 0) {
    Write-Error "An error occurred while committing the latest changes."
    Return $LastExitCode
} Else {
    Write-Host "Finished committing the latest changes."
}

Pop-Location