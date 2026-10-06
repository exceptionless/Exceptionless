#!/bin/bash
set -euo pipefail

cd /app/wwwroot
update-config
cd /app

exec dotnet Exceptionless.Web.dll "$@"
