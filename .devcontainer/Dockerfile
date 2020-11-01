FROM mcr.microsoft.com/dotnet/sdk:5.0

# https://github.com/microsoft/vscode-dev-containers/tree/master/containers/dotnetcore
# try using this as base: mcr.microsoft.com/vscode/devcontainers/dotnetcore

# Update and install some utilities
RUN apt-get update && \
  apt-get install -y curl git gnupg2 jq sudo zsh htop gnupg gnupg-agent software-properties-common apt-transport-https ca-certificates nano

# Install 3.1 sdk as it's needed by some of the vscode tooling
RUN wget https://packages.microsoft.com/config/debian/10/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
  dpkg -i packages-microsoft-prod.deb && \
  apt-get update && \
  apt-get install -y dotnet-sdk-3.1

# Enable local HTTPS
RUN dotnet dev-certs https --trust

# Install nvm
RUN curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.35.3/install.sh | bash

# Install Docker CLI
RUN curl -fsSL https://download.docker.com/linux/debian/gpg | apt-key add - && \
  add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/debian $(lsb_release -cs) stable" && \
  apt-get update && \
  apt-get install -y docker-ce docker-ce-cli containerd.io

# Install docker-compose
RUN curl -L "https://github.com/docker/compose/releases/download/1.27.4/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose && \
  chmod +x /usr/local/bin/docker-compose

# Install kubectl
RUN curl -s https://packages.cloud.google.com/apt/doc/apt-key.gpg | apt-key add - && \
  echo "deb https://apt.kubernetes.io/ kubernetes-xenial main" | tee -a /etc/apt/sources.list.d/kubernetes.list && \
  apt-get update && \
  apt-get install -y kubectl

# Install helm
RUN curl -fsSL -o get_helm.sh https://raw.githubusercontent.com/helm/helm/master/scripts/get-helm-3 && \
  chmod 700 get_helm.sh && \
  ./get_helm.sh

# Update to the latest PowerShell
RUN curl -sSL https://raw.githubusercontent.com/PowerShell/PowerShell/master/tools/install-powershell.sh | bash

# install k6 load testing tool
RUN apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 379CE192D401AB61 && \
  echo "deb https://dl.bintray.com/loadimpact/deb stable main" | tee -a /etc/apt/sources.list && \
  apt-get update && \
  apt-get install k6

# Setup and install oh-my-zsh
RUN sh -c "$(curl -fsSL https://raw.githubusercontent.com/robbyrussell/oh-my-zsh/master/tools/install.sh)" && \
  cp -R /root/.oh-my-zsh /home/$USERNAME && \
  cp /root/.zshrc /home/$USERNAME && \
  sed -i -e "s/\/root\/.oh-my-zsh/\/home\/$USERNAME\/.oh-my-zsh/g" /home/$USERNAME/.zshrc && \
  chown -R $USER_UID:$USER_GID /home/$USERNAME/.oh-my-zsh /home/$USERNAME/.zshrc && \
  chsh -s $(which zsh)

ENTRYPOINT [ "/bin/zsh" ]