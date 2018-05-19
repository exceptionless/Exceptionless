$base_dir = Resolve-Path ".\"
$artifactsDir = "$base_dir\artifacts"
$sourceDir = "$base_dir\src"

If ($env:APPVEYOR_PULL_REQUEST_NUMBER -ne $null) {
    Write-Host "Artifacts will not be created for pull requests."
    Return
}

Write-Host "Cloning repository into $($artifactsDir)..."
git clone "$env:BUILD_REPO_URL" "$artifactsDir" -q 2>&1 | ForEach-Object { "$_" }

If ($LastExitCode -ne 0) {
    Write-Error "An error occurred while cloning the repository."
    Return $LastExitCode
}

Push-Location $artifactsDir

git fetch --all -f -q 2>&1 | ForEach-Object { "$_" }
$branches = (git branch -r) 2> $null
If (($branches.Replace(" ", "").Split([environment]::NewLine) -contains "origin/$($env:APPVEYOR_REPO_BRANCH)") -eq $True) {
    Write-Host "Checking out branch: $env:APPVEYOR_REPO_BRANCH"
    git checkout "$env:APPVEYOR_REPO_BRANCH" -f -q 2>&1 | ForEach-Object { "$_" }
} else {
    Write-Host "Checking out new branch: $env:APPVEYOR_REPO_BRANCH"
    git checkout -b "$env:APPVEYOR_REPO_BRANCH" -q 2>&1 | ForEach-Object { "$_" }
}

If ($LastExitCode -ne 0) {
    Write-Error "An error occurred while changing to branch: $env:APPVEYOR_REPO_BRANCH"
    Return $LastExitCode
}

Write-Host "Removing existing files..."
git rm -r * -q 2>&1 | ForEach-Object { "$_" }

Write-Host "Copying build artifacts..."
ROBOCOPY "$sourceDir\Exceptionless.Web" "$artifactsDir" /XD "$sourceDir\Exceptionless.Web\bin" "$sourceDir\Exceptionless.Web\obj" "$sourceDir\Exceptionless.Web\Properties" /S /XF "*.nuspec" "*.settings" "*.cs" "*.Development.yml" "*.csproj" "*.user" "*.suo" "*.xsd" "*.ide" /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Exceptionless.Web\bin\Release\netcoreapp2.1\publish" "$artifactsDir\bin" /XD "$sourceDir\Exceptionless.Web\bin\Release\netcoreapp2.1\publish\wwwroot" /S /XF "*.yml" "Web.config" /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Jobs\EventPostsJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\bin" /S /XF "EventPostsJob.*" "*.yml" /NFL /NDL /NJH /NJS /nc /ns /np

Write-Host "Copying CleanupSnapshot job..."
ROBOCOPY "$sourceDir\Jobs\CleanupSnapshotJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\triggered\CleanupSnapshot" "CleanupSnapshot*" "appsettings*.yml" "run.bat" "settings.job" /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying CloseInactiveSession job..."
ROBOCOPY "$sourceDir\Jobs\CloseInactiveSessionsJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\continuous\CloseInactiveSession" "CloseInactiveSession*" "appsettings*.yml" "run.bat" "settings.job" /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying DailySummary job..."
ROBOCOPY "$sourceDir\Jobs\DailySummaryJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\continuous\DailySummary" "DailySummary*" "appsettings*.yml" "run.bat" "settings.job" /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying DownloadGeoIPDatabase job..."
ROBOCOPY "$sourceDir\Jobs\DownloadGeoIPDatabaseJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\triggered\DownloadGeoIPDatabase" "DownloadGeoIPDatabase*" "appsettings*.yml" "run.bat" "settings.job" /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying EventNotification job..."
ROBOCOPY "$sourceDir\Jobs\EventNotificationsJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\continuous\EventNotification" "EventNotification*" "appsettings*.yml" "run.bat" /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying EventPost job..."
ROBOCOPY "$sourceDir\Jobs\EventPostsJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\continuous\EventPost" "EventPost*" "appsettings*.yml" "run.bat" /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Jobs\EventPostsJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\continuous\EventPost2" "EventPost*" "appsettings*.yml" "run.bat" /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Jobs\EventPostsJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\continuous\EventPost3" "EventPost*" "appsettings*.yml" "run.bat" /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Jobs\EventPostsJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\continuous\EventPost4" "EventPost*" "appsettings*.yml" "run.bat" /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying EventSnapshot job..."
ROBOCOPY "$sourceDir\Jobs\EventSnapshotJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\triggered\EventSnapshot" "EventSnapshot*" "appsettings*.yml" "run.bat" "settings.job" /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying EventUserDescription job..."
ROBOCOPY "$sourceDir\Jobs\EventUserDescriptionsJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\continuous\EventUserDescription" "EventUserDescription*" "appsettings*.yml" "run.bat" /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying MailMessage job..."
ROBOCOPY "$sourceDir\Jobs\MailMessageJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\continuous\MailMessage" "MailMessage*" "appsettings*.yml" "run.bat" /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying MaintainIndexes job..."
ROBOCOPY "$sourceDir\Jobs\MaintainIndexesJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\triggered\MaintainIndexes" "MaintainIndexes*" "appsettings*.yml" "run.bat" "settings.job" /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying OrganizationSnapshot job..."
ROBOCOPY "$sourceDir\Jobs\OrganizationSnapshotJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\triggered\OrganizationSnapshot" "OrganizationSnapshot*" "appsettings*.yml" "run.bat" "settings.job" /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying RetentionLimit job..."
ROBOCOPY "$sourceDir\Jobs\RetentionLimitsJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\continuous\RetentionLimit" "RetentionLimit*" "appsettings*.yml" "run.bat" "settings.job" /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying StackEventCount job..."
ROBOCOPY "$sourceDir\Jobs\StackEventCountJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\continuous\StackEventCount" "StackEventCount*" "appsettings*.yml" "run.bat" "settings.job" /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying StackSnapshot job..."
ROBOCOPY "$sourceDir\Jobs\StackSnapshotJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\triggered\StackSnapshot" "StackSnapshot*" "appsettings*.yml" "run.bat" "settings.job" /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying WebHook job..."
ROBOCOPY "$sourceDir\Jobs\WebHooksJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\continuous\WebHook" "WebHook*" "appsettings*.yml" "run.bat" /NFL /NDL /NJH /NJS /nc /ns /np
Write-Host "Copying WorkItem job..."
ROBOCOPY "$sourceDir\Jobs\WorkItemJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\continuous\WorkItem" "WorkItem*" "appsettings*.yml" "run.bat" /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Jobs\WorkItemJob\bin\Release\netcoreapp2.1\publish" "$artifactsDir\App_Data\jobs\continuous\WorkItem2" "WorkItem*" "appsettings*.yml" "run.bat" /NFL /NDL /NJH /NJS /nc /ns /np

git add * 2>&1 | ForEach-Object { "$_" }
$res = git diff --cached --numstat | wc -l
If ($res.Trim() -eq "0") {
  Write-Host "No changes since last build."
  Return 0
}

Write-Host "Pushing build to artifacts repository..."
$tag = "build-$($env:APPVEYOR_BUILD_NUMBER)"
git commit -a -m "Build: $env:APPVEYOR_BUILD_NUMBER Author: $env:APPVEYOR_REPO_COMMIT_AUTHOR Branch: $env:APPVEYOR_REPO_BRANCH Commit: $($env:APPVEYOR_REPO_NAME)@$($env:APPVEYOR_REPO_COMMIT)" 2>&1 | ForEach-Object { "$_" }
git push origin "$env:APPVEYOR_REPO_BRANCH" -q 2>&1 | ForEach-Object { "$_" }
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

git push --tags origin $env:APPVEYOR_REPO_BRANCH -q 2>&1 | ForEach-Object { "$_" }

If ($LastExitCode -ne 0) {
  Write-Error "An error occurred while tagging the build."
  Return $LastExitCode
} Else {
  Write-Host "Finished tagging the build."
}

Pop-Location