$base_dir = Resolve-Path ".\..\"   
$artifactsDir = "$base_dir\artifacts"
$sourceDir = "$base_dir\Source"
$exclude = @("*.nuspec","*.settings","*.cs","packages.config","*.csproj","*.user","*.suo", "*.xsd", "bin", "obj", "*.ide")

if (!(Test-Path -Path $artifactsDir)) {
    git clone $env:BUILD_REPO_URL $artifactsDir
    Push-Location $artifactsDir
} else { 
    Push-Location $artifactsDir
    git pull
}

git rm -r *

Copy-Item -Path "$sourceDir\Api\*" -Destination $artifactsDir -Recurse -Exclude $exclude -ErrorAction SilentlyContinue 
Copy-Item -Path "$base_dir\packages\Foundatio.*\tools\Job.exe" -Destination "$artifactsDir\App_Data\JobRunner\" 
Copy-Item -Path "$base_dir\packages\Foundatio.*\tools\Job.pdb" -Destination "$artifactsDir\App_Data\JobRunner\" 
Copy-Item -Path "$sourceDir\Api\bin\*" -Destination "$artifactsDir\App_Data\JobRunner\bin\" -Recurse -Exclude $exclude -ErrorAction SilentlyContinue
Copy-Item -Path "$sourceDir\WebJobs\App.config" -Destination "$artifactsDir\App_Data\JobRunner\Job.exe.config" 
Copy-Item -Path "$sourceDir\WebJobs\Job.bat" -Destination "$artifactsDir\App_Data\JobRunner\"
Copy-Item -Path "$sourceDir\WebJobs\NLog.config" -Destination "$artifactsDir\App_Data\JobRunner\"
Copy-Item -Path "$sourceDir\WebJobs\NLog.config" -Destination "$artifactsDir\App_Data\JobRunner\bin\"
Copy-Item -Path "$sourceDir\Migrations\EventMigration\bin\Release\Exceptionless.EventMigration*.*" -Destination "$artifactsDir\App_Data\JobRunner\bin\" -Recurse -Exclude $exclude -ErrorAction SilentlyContinue
Copy-Item -Path "$sourceDir\WebJobs\continuous\*" -Destination "App_Data\jobs\continuous\" -Recurse -ErrorAction SilentlyContinue
Copy-Item -Path "$sourceDir\WebJobs\triggered\*" -Destination "App_Data\jobs\triggered\" -Recurse -ErrorAction SilentlyContinue

git add *
git commit -a -m "Build $env:APPVEYOR_BUILD_VERSION"
git push origin master

Pop-Location