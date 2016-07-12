Function Start-Website([string] $path = $(throw "Path is required."), [string] $port = $(throw "Port is required.")) {
    $iisExpressExe =  "$env:programfiles\IIS Express\iisexpress.exe"
    If (!(Test-Path -Path $iisExpressExe)) {
        $iisExpressExe =  "${env:programfiles(x86)}\IIS Express\iisexpress.exe"
    }
    
    If (!(Test-Path -Path $iisExpressExe)) {
        Write-Error "Please install IIS Express to continue"
        Return;
    }
    
    Write-host "Starting site on port: $port"
    cmd /c start cmd /k "$iisExpressExe" "/port:$port" "/path:$path"
    Start-Sleep -m 1000
    Write-Host "Site started on http://localhost:$port"
}

$wwwroot = Resolve-path ".\wwwroot"
Start-Website $wwwroot 50000
Start-Process "http://localhost:50000"