FROM mcr.microsoft.com/devcontainers/dotnet:0-7.0

RUN apt-get clean -y && rm -rf /var/lib/apt/lists/* /tmp/library-scripts

RUN apt-get update && export DEBIAN_FRONTEND=noninteractive \
    && apt-get -y install --no-install-recommends libnss3-tools htop nano curl jq

RUN sudo dotnet dev-certs https \
    && sudo -E dotnet dev-certs https -ep /usr/local/share/ca-certificates/aspnet/https.crt --format PEM \
    && sudo update-ca-certificates

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