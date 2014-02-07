# Copyright 2014 Exceptionless
#
# This program is free software: you can redistribute it and/or modify it 
# under the terms of the GNU Affero General Public License as published 
# by the Free Software Foundation, either version 3 of the License, or 
# (at your option) any later version.
# 
#     http://www.gnu.org/licenses/agpl-3.0.html

[Environment]::SetEnvironmentVariable("BUILD_NUMBER", "2000", "Machine")
[Environment]::SetEnvironmentVariable("BUILD_VCS_NUMBER_Exceptionless_Master", "c9d183c8570143142ca61c555360e7f0732efc09", "Machine")
[Environment]::SetEnvironmentVariable("info_version_suffix", "", "Machine")

[Environment]::SetEnvironmentVariable("msbuild_deploy_user", $null, "Machine")
[Environment]::SetEnvironmentVariable("msbuild_deploy_password", $null, "Machine")
[Environment]::SetEnvironmentVariable("nuget_api_key", $null, "Machine")