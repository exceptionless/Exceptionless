$base_dir = Resolve-Path ".\"   
$artifactsDir = "$base_dir\artifacts"
$sourceDir = "$base_dir\src"

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
ROBOCOPY "$sourceDir\Exceptionless.Api" "$artifactsDir" /XD "$sourceDir\Exceptionless.Api\obj" "$sourceDir\Exceptionless.Api\App_Data" /S /XF "*.nuspec" "*.settings" "*.cs" "packages.config" "*.csproj" "*.user" "*.suo" "*.xsd" "*.ide" /NFL /NDL /NJH /NJS /nc /ns /np

Write-Host "Copying CleanupSnapshot job..."
ROBOCOPY "$sourceDir\Jobs\CleanupSnapshot\bin\Release" "$artifactsDir\App_Data\jobs\triggered\CleanupSnapshot" /XD "$sourceDir\Jobs\CleanupSnapshot\bin\Release\bin" /S /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying CloseInactiveSession job..."
ROBOCOPY "$sourceDir\Jobs\CloseInactiveSession\bin\Release" "$artifactsDir\App_Data\jobs\continuous\CloseInactiveSession" /XD "$sourceDir\Jobs\CloseInactiveSession\bin\Release\bin" /S /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying DailySummary job..."
ROBOCOPY "$sourceDir\Jobs\DailySummary\bin\Release" "$artifactsDir\App_Data\jobs\continuous\DailySummary" /XD "$sourceDir\Jobs\DailySummary\bin\Release\bin" /S /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying DownloadGeoIPDatabase job..."
ROBOCOPY "$sourceDir\Jobs\DownloadGeoIPDatabase\bin\Release" "$artifactsDir\App_Data\jobs\triggered\DownloadGeoIPDatabase" /XD "$sourceDir\Jobs\DownloadGeoIPDatabase\bin\Release\bin" /S /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying EventNotification job..."
ROBOCOPY "$sourceDir\Jobs\EventNotification\bin\Release" "$artifactsDir\App_Data\jobs\continuous\EventNotification" /XD "$sourceDir\Jobs\EventNotification\bin\Release\bin" /S /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying EventPost job..."
ROBOCOPY "$sourceDir\Jobs\EventPost\bin\Release" "$artifactsDir\App_Data\jobs\continuous\EventPost" /XD "$sourceDir\Jobs\EventPost\bin\Release\bin" /S /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying EventSnapshot job..."
ROBOCOPY "$sourceDir\Jobs\EventSnapshot\bin\Release" "$artifactsDir\App_Data\jobs\triggered\EventSnapshot" /XD "$sourceDir\Jobs\EventSnapshot\bin\Release\bin" /S /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying EventUserDescription job..."
ROBOCOPY "$sourceDir\Jobs\EventUserDescription\bin\Release" "$artifactsDir\App_Data\jobs\continuous\EventUserDescription" /XD "$sourceDir\Jobs\EventUserDescription\bin\Release\bin" /S /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying MailMessage job..."
ROBOCOPY "$sourceDir\Jobs\MailMessage\bin\Release" "$artifactsDir\App_Data\jobs\continuous\MailMessage" /XD "$sourceDir\Jobs\MailMessage\bin\Release\bin" /S /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying MaintainIndexes job..."
ROBOCOPY "$sourceDir\Jobs\MaintainIndexes\bin\Release" "$artifactsDir\App_Data\jobs\triggered\MaintainIndexes" /XD "$sourceDir\Jobs\MaintainIndexes\bin\Release\bin" /S /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying OrganizationSnapshot job..."
ROBOCOPY "$sourceDir\Jobs\OrganizationSnapshot\bin\Release" "$artifactsDir\App_Data\jobs\triggered\OrganizationSnapshot" /XD "$sourceDir\Jobs\OrganizationSnapshot\bin\Release\bin" /S /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying RetentionLimit job..."
ROBOCOPY "$sourceDir\Jobs\RetentionLimit\bin\Release" "$artifactsDir\App_Data\jobs\continuous\RetentionLimit" /XD "$sourceDir\Jobs\RetentionLimit\bin\Release\bin" /S /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying StackSnapshot job..."
ROBOCOPY "$sourceDir\Jobs\StackSnapshot\bin\Release" "$artifactsDir\App_Data\jobs\triggered\StackSnapshot" /XD "$sourceDir\Jobs\StackSnapshot\bin\Release\bin" /S /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying WebHook job..."
ROBOCOPY "$sourceDir\Jobs\WebHook\bin\Release" "$artifactsDir\App_Data\jobs\continuous\WebHook" /XD "$sourceDir\Jobs\WebHook\bin\Release\bin" /S /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying WorkItem job..."
ROBOCOPY "$sourceDir\Jobs\WorkItem\bin\Release" "$artifactsDir\App_Data\jobs\continuous\WorkItem" /XD "$sourceDir\Jobs\WorkItem\bin\Release\bin" /S /NFL /NDL /NJH /NJS /nc /ns /np

Write-Host "Copying Functions"
ROBOCOPY "$sourceDir\Jobs" "$artifactsDir" host.json /S /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Jobs\Exceptionless.AzureFunctions\bin\Release" "$artifactsDir\bin" /XD "$sourceDir\Jobs\Exceptionless.AzureFunctions\bin\Release\bin" /S /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Jobs\CleanupSnapshot" "$artifactsDir\CleanupSnapshot" function.json /S /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Jobs\CloseInactiveSession" "$artifactsDir\CloseInactiveSession" function.json /S /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Jobs\DailySummary" "$artifactsDir\DailySummary" function.json /S /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Jobs\DownloadGeoIPDatabase" "$artifactsDir\DownloadGeoIPDatabase" function.json /S /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Jobs\EventNotification" "$artifactsDir\EventNotification" function.json /S /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Jobs\EventPost" "$artifactsDir\EventPost" function.json /S /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Jobs\EventSnapshot" "$artifactsDir\EventSnapshot" function.json /S /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Jobs\EventUserDescription" "$artifactsDir\EventUserDescription" function.json /S /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Jobs\MailMessage" "$artifactsDir\MailMessage" function.json /S /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Jobs\MaintainIndexes" "$artifactsDir\MaintainIndexes" function.json /S /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Jobs\RetentionLimit" "$artifactsDir\RetentionLimit" function.json /S /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Jobs\StackSnapshot" "$artifactsDir\StackSnapshot" function.json /S /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Jobs\WebHook" "$artifactsDir\WebHook" function.json /S /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Jobs\WorkItem" "$artifactsDir\WorkItem" function.json /S /NFL /NDL /NJH /NJS /nc /ns /np

git add * 2>&1 | %{ "$_" }
$res = git diff --cached --numstat | wc -l
If ($res.Trim() -eq "0") {
  Write-Host "No changes since last build."
  Return 0
}

Write-Host "Pushing build to artifacts repository..."
$tag = "build-$($env:APPVEYOR_BUILD_NUMBER)"
git commit -a -m "Build: $env:APPVEYOR_BUILD_NUMBER Author: $env:APPVEYOR_REPO_COMMIT_AUTHOR Branch: $env:APPVEYOR_REPO_BRANCH Commit: $($env:APPVEYOR_REPO_NAME)@$($env:APPVEYOR_REPO_COMMIT)" 2>&1 | %{ "$_" }
git push origin "$env:APPVEYOR_REPO_BRANCH" -q 2>&1 | %{ "$_" }
If ($LastExitCode -ne 0) {
  Write-Error "An error occurred while pushing the build."
  Return $LastExitCode
} Else {
  Write-Host "Finished pushing the build."
}

Write-Host "Tagging $tag..."
git tag $tag $env:APPVEYOR_REPO_BRANCH 2>&1
If ($LastExitCode -ne 0) {
  Write-Error "An error occurred while tagging the build."
  Return $LastExitCode
}

git push --tags origin $env:APPVEYOR_REPO_BRANCH -q 2>&1 | %{ "$_" }

If ($LastExitCode -ne 0) {
  Write-Error "An error occurred while tagging the build."
  Return $LastExitCode
} Else {
  Write-Host "Finished tagging the build."
}

Pop-Location