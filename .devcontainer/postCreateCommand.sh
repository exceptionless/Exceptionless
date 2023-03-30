#!/bin/bash

dotnet dev-certs https --trust
dotnet restore Exceptionless.sln
