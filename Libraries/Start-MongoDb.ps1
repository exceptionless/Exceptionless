$mongo_version = "2.6.8"
if ($env:MONGO_VERSION) {
	$mongo_version = $env:MONGO_VERSION
}

Push-Location $PSScriptRoot

if (!(Test-Path -Path "MongoDB") -And !(Test-Path -Path "mongodb.msi")) {
    wget "https://fastdl.mongodb.org/win32/mongodb-win32-x86_64-2008plus-$mongo_version-signed.msi" -OutFile 'mongodb.msi'
}

if (Test-Path -Path "mongodb.msi") {
    $list = 
    @(
        "/I `"$(Get-Location)\mongodb.msi`"",
        "/QN",
        "/L*V `"$(Get-Location)\mongodb-install.log`"",
        "INSTALLLOCATION=`"$(Get-Location)\MongoDB`""
    )

    Start-Process -FilePath "msiexec" -ArgumentList $list -Wait
    rm .\mongodb.msi
    if (!(Test-Path -Path "$(Get-Location)\MongoDB\data")) {
        mkdir "$(Get-Location)\MongoDB\data" | Out-Null
    }
}

Start-Process -NoNewWindow "$(Get-Location)\MongoDB\bin\mongod.exe" -ArgumentList "-dbpath $(Get-Location)\MongoDB\data"

Pop-Location
