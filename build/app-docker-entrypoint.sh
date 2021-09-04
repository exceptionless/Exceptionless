#!/bin/bash

pushd /app/wwwroot
update-config
popd

eval "dotnet Exceptionless.Web.dll $@"
