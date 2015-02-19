# Copyright 2014 Exceptionless
#
# This program is free software: you can redistribute it and/or modify it 
# under the terms of the GNU Affero General Public License as published 
# by the Free Software Foundation, either version 3 of the License, or 
# (at your option) any later version.
# 
#     http://www.gnu.org/licenses/agpl-3.0.html

param($task = "default", $properties = @{})

$scriptPath = $MyInvocation.MyCommand.Path
$scriptDir = Split-Path $scriptPath

nuget\NuGet.exe install nuget\packages.config -OutputDirectory packages
nuget\NuGet.exe restore Exceptionless.sln

Import-Module (Get-ChildItem "$scriptDir\packages\psake.*\tools\psake.psm1" | Select-Object -First 1)

$psake.use_exit_on_error = $true
Invoke-psake "$scriptDir\default.ps1" $task -properties $properties

if ($psake.build_success -eq $false) { exit 1 } else { exit 0 }