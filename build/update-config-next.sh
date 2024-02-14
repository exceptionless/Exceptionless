#!/bin/bash

ApiUrl="${EX_ApiUrl:-}"
EnableSsl="${EX_EnableSsl:-false}"
EnableAccountCreation="${EX_EnableAccountCreation:-true}"

OAuth="${EX_ConnectionStrings__OAuth:-}"
IFS=';' read -a oauthParts <<< "$OAuth"
for part in ${oauthParts[@]}
do
  key="$( cut -d '=' -f 1 <<< $part )"; echo "key: $key"
  value="$( cut -d '=' -f 2- <<< $part )"; echo "value: $value"

  if [ "$key" == "FacebookId" ]; then
    FacebookAppId=$value
  fi
  if [ "$key" == "GitHubId" ]; then
    GitHubAppId=$value
  fi
  if [ "$key" == "GoogleId" ]; then
    GoogleAppId=$value
  fi
  if [ "$key" == "IntercomId" ]; then
    IntercomAppId=$value
  fi
  if [ "$key" == "MicrosoftId" ]; then
    MicrosoftAppId=$value
  fi
  if [ "$key" == "SlackId" ]; then
    SlackAppId=$value
  fi
done

config_header="(function (window) {
    'use strict';

    window.__env = {"

config="
    PUBLIC_BASE_URL: '$ApiUrl' || window.location.origin,
    PUBLIC_USE_SSL: $EnableSsl,
    PUBLIC_ENABLE_ACCOUNT_CREATION: $EnableAccountCreation,
    PUBLIC_SYSTEM_NOTIFICATION_MESSAGE: '$EX_NotificationMessage',
    PUBLIC_EXCEPTIONLESS_API_KEY: '$EX_ExceptionlessApiKey',
    PUBLIC_EXCEPTIONLESS_SERVER_URL: '$EX_ExceptionlessServerUrl',
    PUBLIC_STRIPE_PUBLISHABLE_KEY: '$EX_StripePublishableApiKey',
    PUBLIC_FACEBOOK_APPID: '$FacebookAppId',
    PUBLIC_GITHUB_APPID: '$GitHubAppId',
    PUBLIC_GOOGLE_APPID: '$GoogleAppId',
    PUBLIC_MICROSOFT_APPID: '$MicrosoftAppId',
    PUBLIC_INTERCOM_APPID: '$IntercomAppId',
    PUBLIC_SLACK_APPID: '$SlackAppId'"

config_footer="
    };
})(this);"

echo "Exceptionless UI Config"
echo "$config"

checksum=`echo -n $config | md5sum | cut -c 1-32`
echo "$config_header$config$config_footer" > "runtime-env.$checksum.js"

CONTENT=$(cat index.html)
echo "$CONTENT" | sed -E "s/runtime-env\..+\.js/runtime-env.$checksum.js/" > index.html
