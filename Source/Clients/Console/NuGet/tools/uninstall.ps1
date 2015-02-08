# Copyright 2014 Exceptionless
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# 
#      http://www.apache.org/licenses/LICENSE-2.0

param($installPath, $toolsPath, $package, $project)

Import-Module (Join-Path $toolsPath exceptionless.psm1)

$configPath = find_config $project

if ($configPath -ne $null) {
	remove_config $configPath
}
