# get current IP
$ip = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object {$_.InterfaceAlias -like '*ethernet*'}).IPAddress

# generate password
$randomObj = New-Object System.Random
$newPassword = ""
1..12 | ForEach { $newPassword = $newPassword + [char]$randomObj.next(33,126) }

# change password
$objUser = [ADSI]("WinNT://$($env:computername)/appveyor")
$objUser.SetPassword($newPassword)

# allow RDP on firewall
Enable-NetFirewallRule -DisplayName 'Remote Desktop - User Mode (TCP-in)'

# place "lock" file
$path = "$($env:USERPROFILE)\Desktop\Delete me to continue build.txt"
Set-Content -Path $path -Value ''

Write-Warning "To connect this build worker via RDP:"
Write-Warning "Server: $ip"
Write-Warning "Username: appveyor"
Write-Warning "Password: $newPassword"
Write-Warning "There is 'Delete me to continue build.txt' file has been created on Desktop - delete it to continue the build."

while($true) { if (-not (Test-Path $path)) { break; } else { Start-Sleep -Seconds 1 } }