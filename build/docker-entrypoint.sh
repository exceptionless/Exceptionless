#!/bin/bash

# ensure the correct owner for the Elasticsearch data directory
chown -R elasticsearch:elasticsearch /usr/share/elasticsearch/data

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

    sed -i -E "s/app\/app\./app\/wwwroot\/app\./" /usr/local/bin/bootstrap
    sed -i -E "s/app\/index\./app\/wwwroot\/index\./" /usr/local/bin/bootstrap
    sed -i -E "s/echo \"Running NGINX\"//" /usr/local/bin/bootstrap
    sed -i -E "s/nginx//" /usr/local/bin/bootstrap
    /usr/local/bin/bootstrap

    supervisord -c /etc/supervisord.conf

    while [ ! -f /var/log/supervisor/elasticsearch.log ]; do sleep 1; done
    while [ ! -f /var/log/supervisor/exceptionless.log ]; do sleep 1; done

    tail -f /var/log/supervisor/*.log
fi