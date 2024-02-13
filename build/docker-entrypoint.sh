#!/bin/bash

# ensure the correct owner for the Elasticsearch data directory
chown -R elasticsearch:elasticsearch /usr/share/elasticsearch/data
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$PATH:$HOME/.dotnet"

if [ "$#" -ne 0 ]; then
    echo "Running Exceptionless Job $@"

    mkdir -p /var/log/exceptionless

    echo "Starting Elasticsearch..."
    /usr/local/bin/docker-entrypoint.sh eswrapper > /var/log/exceptionless/elasticsearch.log 2>&1 &
    sleep 5
    eval "dotnet Exceptionless.Job.dll $@"
else
    echo "Running Exceptionless Web"

    mkdir -p /var/log/supervisor
    mkdir -p Temp/

    pushd /app/wwwroot
    update-config
    popd

    pushd /app/wwwroot/next
    update-config-next
    popd

    supervisord -c /etc/supervisord.conf

    while [ ! -f /var/log/supervisor/elasticsearch.log ]; do sleep 1; done
    while [ ! -f /var/log/supervisor/exceptionless.log ]; do sleep 1; done

    tail -f /var/log/supervisor/*.log
fi
