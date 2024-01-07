FROM mcr.microsoft.com/devcontainers/dotnet:0-8.0

RUN apt-get clean -y && rm -rf /var/lib/apt/lists/* /tmp/library-scripts

RUN apt-get update && export DEBIAN_FRONTEND=noninteractive \
    && apt-get -y install --no-install-recommends libnss3-tools htop nano curl jq

RUN sudo dotnet dev-certs https \
    && sudo -E dotnet dev-certs https -ep /usr/local/share/ca-certificates/aspnet/https.crt --format PEM \
    && sudo update-ca-certificates

# Change shell to zsh
RUN chsh -s $(which zsh)

ENTRYPOINT [ "/bin/zsh" ]
