FROM mcr.microsoft.com/dotnet/sdk:5.0

# Update and install some utilities
RUN apt-get update && \
  apt-get install -y curl git gnupg2 jq sudo zsh htop

# install 3.1 sdk as it's needed by some of the vscode tooling
RUN wget https://packages.microsoft.com/config/ubuntu/19.10/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
  dpkg -i packages-microsoft-prod.deb && \
  apt-get update && \
  apt-get install -y apt-transport-https gnupg && \
  apt-get update && \
  apt-get install -y dotnet-sdk-3.1

## Enable local HTTPS
RUN dotnet dev-certs https --trust

## Instsall nvm
RUN curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.35.3/install.sh | bash

## Install Docker CLI
RUN curl -fsSL https://get.docker.com | bash

## Update to the latest PowerShell
RUN curl -sSL https://raw.githubusercontent.com/PowerShell/PowerShell/master/tools/install-powershell.sh | bash

# install k6 load testing tool
RUN apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 379CE192D401AB61 && \
  echo "deb https://dl.bintray.com/loadimpact/deb stable main" | tee -a /etc/apt/sources.list && \
  apt-get update && \
  apt-get install k6

## Setup and install oh-my-zsh
RUN sh -c "$(curl -fsSL https://raw.githubusercontent.com/robbyrussell/oh-my-zsh/master/tools/install.sh)" && \
  cp -R /root/.oh-my-zsh /home/$USERNAME && \
  cp /root/.zshrc /home/$USERNAME && \
  sed -i -e "s/\/root\/.oh-my-zsh/\/home\/$USERNAME\/.oh-my-zsh/g" /home/$USERNAME/.zshrc && \
  chown -R $USER_UID:$USER_GID /home/$USERNAME/.oh-my-zsh /home/$USERNAME/.zshrc
