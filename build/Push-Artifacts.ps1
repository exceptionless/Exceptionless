$base_dir = Resolve-Path ".\"
$artifactsDir = "$base_dir\artifacts"
$sourceDir = "$base_dir\src"

If ($env:APPVEYOR_PULL_REQUEST_NUMBER -ne $null) {
    Write-Host "Artifacts will not be created for pull requests."
    Return
}

Write-Host "Cloning repository into $($artifactsDir)..."
git clone "$env:BUILD_REPO_URL" "$artifactsDir" --depth 1 -q 2>&1 | % { "$_" }

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
ROBOCOPY "$sourceDir\Exceptionless.Web" "$artifactsDir" /XD "$sourceDir\Exceptionless.Web\bin" "$sourceDir\Exceptionless.Web\obj" "$sourceDir\Exceptionless.Web\Properties" /S /XF "*.nuspec" "*.settings" "*.cs" "*.Development.yml" "*.csproj" "*.user" "*.suo" "*.xsd" "*.ide" /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Exceptionless.Web\bin\Release\netcoreapp2.0\publish" "$artifactsDir\bin" /XD "$sourceDir\Exceptionless.Web\bin\Release\netcoreapp2.0\publish\wwwroot" /S /XF "*.yml" "Web.config" /NFL /NDL /NJH /NJS /nc /ns /np
ROBOCOPY "$sourceDir\Exceptionless.Insulation\bin\Release\netcoreapp2.0\publish" "$artifactsDir\bin" /XD /XF "Exceptionless.Insulation.*" /NFL /NDL /NJH /NJS /nc /ns /np

Write-Host "Copying CleanupSnapshot job..."
ROBOCOPY "$sourceDir\Jobs\CleanupSnapshot\bin\Release\netcoreapp2.0" "$artifactsDir\App_Data\jobs\triggered\CleanupSnapshot" /XD "$sourceDir\Jobs\CleanupSnapshot\bin\Release\netcoreapp2.0\publish" /S /NFL /NDL /NJH /NJS /nc /ns /np /XF "Exceptionless.*" "System.*"
Write-Host "Copying CloseInactiveSession job..."
ROBOCOPY "$sourceDir\Jobs\CloseInactiveSession\bin\Release\netcoreapp2.0" "$artifactsDir\App_Data\jobs\continuous\CloseInactiveSession" /XD "$sourceDir\Jobs\CloseInactiveSession\bin\Release\netcoreapp2.0\publish" /S /NFL /NDL /NJH /NJS /nc /ns /np /XF "Exceptionless.*" "System.*"
Write-Host "Copying DailySummary job..."
ROBOCOPY "$sourceDir\Jobs\DailySummary\bin\Release\netcoreapp2.0" "$artifactsDir\App_Data\jobs\continuous\DailySummary" /XD "$sourceDir\Jobs\DailySummary\bin\Release\netcoreapp2.0\publish" /S /NFL /NDL /NJH /NJS /nc /ns /np /XF "Exceptionless.*" "System.*"
Write-Host "Copying DownloadGeoIPDatabase job..."
ROBOCOPY "$sourceDir\Jobs\DownloadGeoIPDatabase\bin\Release\netcoreapp2.0" "$artifactsDir\App_Data\jobs\triggered\DownloadGeoIPDatabase" /XD "$sourceDir\Jobs\DownloadGeoIPDatabase\bin\Release\netcoreapp2.0\publish" /S /NFL /NDL /NJH /NJS /nc /ns /np /XF "Exceptionless.*" "System.*"
Write-Host "Copying EventNotification job..."
ROBOCOPY "$sourceDir\Jobs\EventNotification\bin\Release\netcoreapp2.0" "$artifactsDir\App_Data\jobs\continuous\EventNotification" /XD "$sourceDir\Jobs\EventNotification\bin\Release\netcoreapp2.0\publish" /S /NFL /NDL /NJH /NJS /nc /ns /np /XF "Exceptionless.*" "System.*"
Write-Host "Copying EventPost job..."
ROBOCOPY "$sourceDir\Jobs\EventPost\bin\Release\netcoreapp2.0" "$artifactsDir\App_Data\jobs\continuous\EventPost" /XD "$sourceDir\Jobs\EventPost\bin\Release\netcoreapp2.0\publish"  /S /NFL /NDL /NJH /NJS /nc /ns /np /XF "Exceptionless.*" "System.*"
ROBOCOPY "$sourceDir\Jobs\EventPost\bin\Release\netcoreapp2.0" "$artifactsDir\App_Data\jobs\continuous\EventPost2" /XD "$sourceDir\Jobs\EventPost\bin\Release\netcoreapp2.0\publish" /S /NFL /NDL /NJH /NJS /nc /ns /np /XF "Exceptionless.*" "System.*"
ROBOCOPY "$sourceDir\Jobs\EventPost\bin\Release\netcoreapp2.0" "$artifactsDir\App_Data\jobs\continuous\EventPost3" /XD "$sourceDir\Jobs\EventPost\bin\Release\netcoreapp2.0\publish" /S /NFL /NDL /NJH /NJS /nc /ns /np /XF "Exceptionless.*" "System.*"
ROBOCOPY "$sourceDir\Jobs\EventPost\bin\Release\netcoreapp2.0" "$artifactsDir\App_Data\jobs\continuous\EventPost4" /XD "$sourceDir\Jobs\EventPost\bin\Release\netcoreapp2.0\publish" /S /NFL /NDL /NJH /NJS /nc /ns /np /XF "Exceptionless.*" "System.*"
Write-Host "Copying EventSnapshot job..."
ROBOCOPY "$sourceDir\Jobs\EventSnapshot\bin\Release\netcoreapp2.0" "$artifactsDir\App_Data\jobs\triggered\EventSnapshot" /XD "$sourceDir\Jobs\EventSnapshot\bin\Release\netcoreapp2.0\publish" /S /NFL /NDL /NJH /NJS /nc /ns /np /XF "Exceptionless.*" "System.*"
Write-Host "Copying EventUserDescription job..."
ROBOCOPY "$sourceDir\Jobs\EventUserDescription\bin\Release\netcoreapp2.0" "$artifactsDir\App_Data\jobs\continuous\EventUserDescription" /XD "$sourceDir\Jobs\EventUserDescription\bin\Release\netcoreapp2.0\publish" /S /NFL /NDL /NJH /NJS /nc /ns /np /XF "Exceptionless.*" "System.*"
Write-Host "Copying MailMessage job..."
ROBOCOPY "$sourceDir\Jobs\MailMessage\bin\Release\netcoreapp2.0" "$artifactsDir\App_Data\jobs\continuous\MailMessage" /XD "$sourceDir\Jobs\MailMessage\bin\Release\netcoreapp2.0\publish" /S /NFL /NDL /NJH /NJS /nc /ns /np /XF "Exceptionless.*" "System.*"
Write-Host "Copying MaintainIndexes job..."
ROBOCOPY "$sourceDir\Jobs\MaintainIndexes\bin\Release\netcoreapp2.0" "$artifactsDir\App_Data\jobs\triggered\MaintainIndexes" /XD "$sourceDir\Jobs\MaintainIndexes\bin\Release\netcoreapp2.0\publish" /S /NFL /NDL /NJH /NJS /nc /ns /np /XF "Exceptionless.*" "System.*"
Write-Host "Copying OrganizationSnapshot job..."
ROBOCOPY "$sourceDir\Jobs\OrganizationSnapshot\bin\Release\netcoreapp2.0" "$artifactsDir\App_Data\jobs\triggered\OrganizationSnapshot" /XD "$sourceDir\Jobs\OrganizationSnapshot\bin\Release\netcoreapp2.0\publish" /S /NFL /NDL /NJH /NJS /nc /ns /np /XF "Exceptionless.*" "System.*"
Write-Host "Copying RetentionLimit job..."
ROBOCOPY "$sourceDir\Jobs\RetentionLimit\bin\Release\netcoreapp2.0" "$artifactsDir\App_Data\jobs\continuous\RetentionLimit" /XD "$sourceDir\Jobs\RetentionLimit\bin\Release\netcoreapp2.0\publish" /S /NFL /NDL /NJH /NJS /nc /ns /np /XF "Exceptionless.*" "System.*"
Write-Host "Copying StackSnapshot job..."
ROBOCOPY "$sourceDir\Jobs\StackSnapshot\bin\Release\netcoreapp2.0" "$artifactsDir\App_Data\jobs\triggered\StackSnapshot" /XD "$sourceDir\Jobs\StackSnapshot\bin\Release\netcoreapp2.0\publish" /S /NFL /NDL /NJH /NJS /nc /ns /np /XF "Exceptionless.*" "System.*"
Write-Host "Copying WebHook job..."
ROBOCOPY "$sourceDir\Jobs\WebHook\bin\Release\netcoreapp2.0" "$artifactsDir\App_Data\jobs\continuous\WebHook" /XD "$sourceDir\Jobs\WebHook\bin\Release\netcoreapp2.0\publish" /S /NFL /NDL /NJH /NJS /nc /ns /np /XF "Exceptionless.*" "System.*"
Write-Host "Copying WorkItem job..."
ROBOCOPY "$sourceDir\Jobs\WorkItem\bin\Release\netcoreapp2.0" "$artifactsDir\App_Data\jobs\continuous\WorkItem" /XD "$sourceDir\Jobs\WorkItem\bin\Release\netcoreapp2.0\publish" /S /NFL /NDL /NJH /NJS /nc /ns /np /XF "Exceptionless.*" "System.*"
ROBOCOPY "$sourceDir\Jobs\WorkItem\bin\Release\netcoreapp2.0" "$artifactsDir\App_Data\jobs\continuous\WorkItem2" /XD "$sourceDir\Jobs\WorkItem\bin\Release\netcoreapp2.0\publish" /S /NFL /NDL /NJH /NJS /nc /ns /np /XF "Exceptionless.*" "System.*"

git add * 2>&1 | %{ "$_" }
$res = git diff --cached --numstat | wc -l
If ($res.Trim() -eq "0") {
  Write-Host "No changes since last build."
  Return 0
}



Pop-Location