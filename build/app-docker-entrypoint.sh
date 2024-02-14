#!/bin/bash

pushd /app/wwwroot
update-config
popd

pushd /app/wwwroot/next
update-config-next
popd

eval "dotnet Exceptionless.Web.dll $@"
