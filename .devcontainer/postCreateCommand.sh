#!/bin/bash
set -euo pipefail

export PATH="$HOME/.dotnet/tools:$PATH"

if dotnet tool list --global | grep -Eiq '^aspire\.cli[[:space:]]'; then
	dotnet tool update --global Aspire.Cli --version 13.2.3
else
	dotnet tool install --global Aspire.Cli --version 13.2.3
fi

export SSL_CERT_DIR="$HOME/.aspnet/dev-certs/trust:/etc/ssl/certs${SSL_CERT_DIR:+:$SSL_CERT_DIR}"
aspire --version
dotnet dev-certs https

if ! dotnet dev-certs https --trust; then
	echo "dotnet dev-certs https --trust could not fully configure trust in this devcontainer; continuing."
fi

dotnet restore Exceptionless.slnx
npm ci --prefix src/Exceptionless.Web/ClientApp
npm ci --prefix src/Exceptionless.Web/ClientApp.angular
