FROM mcr.microsoft.com/devcontainers/base:ubuntu

RUN apt-get clean -y && rm -rf /var/lib/apt/lists/* /tmp/library-scripts

RUN apt-get update && export DEBIAN_FRONTEND=noninteractive \
    && apt-get -y install --no-install-recommends libnss3-tools htop nano curl jq zsh

# Change shell to zsh
RUN chsh -s $(which zsh)

ENTRYPOINT [ "/bin/zsh" ]
