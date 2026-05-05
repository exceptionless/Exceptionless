#!/bin/bash
set -euo pipefail

# Ensure the certificate directories exist before postStartCommand runs dev-certs.
mkdir -p "$HOME/.aspnet/https" "$HOME/.aspnet/dev-certs/trust"

dotnet restore Exceptionless.slnx
npm ci --prefix src/Exceptionless.Web/ClientApp
npm ci --prefix src/Exceptionless.Web/ClientApp.angular
