#!/bin/bash

if [ "$#" -ne 0 ]; then
    exec "$@"
else
    mkdir -p /var/log/supervisor
    mkdir -p Temp/

    sed -i -E "s/app\//app\/wwwroot\//" /usr/local/bin/bootstrap
    sed -i -E "s/nginx//" /usr/local/bin/bootstrap
    /usr/local/bin/bootstrap

    supervisord -c /etc/supervisord.conf
    sleep 3

    tail -f /var/log/supervisor/*.log
fi