#!/bin/bash
set -euo pipefail

if ! command -v aspire >/dev/null 2>&1; then
	curl -sSL https://aspire.dev/install.sh | bash
fi

export PATH="$HOME/.aspire/bin:$PATH"
export SSL_CERT_DIR="$HOME/.aspnet/dev-certs/trust:/etc/ssl/certs${SSL_CERT_DIR:+:$SSL_CERT_DIR}"
aspire --version
dotnet dev-certs https

if ! dotnet dev-certs https --trust; then
	echo "dotnet dev-certs https --trust could not fully configure trust in this devcontainer; continuing."
fi

dotnet restore Exceptionless.slnx
