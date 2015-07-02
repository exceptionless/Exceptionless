$base_dir = Resolve-Path ".\"   
$artifactsDir = "$base_dir\artifacts"
$sourceDir = "$base_dir\Source"

if (!(Test-Path -Path $artifactsDir)) {
    git clone $env:BUILD_REPO_URL $artifactsDir -q
    Push-Location $artifactsDir
} else { 
    Push-Location $artifactsDir
    git pull -q
}

git rm -r *

ROBOCOPY "$sourceDir\Api" $artifactsDir /XD "$sourceDir\Api\obj" "$sourceDir\Api\App_Data" /S /XF "*.nuspec" "*.settings" "*.cs" "packages.config" "*.csproj" "*.user" "*.suo" "*.xsd" "*.ide" > log:nul
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

git add *  -q
git commit -a -m "Build $env:APPVEYOR_BUILD_VERSION" -q
git push origin master -q

Pop-Location