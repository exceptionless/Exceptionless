#!/bin/bash

ApiUrl="${EX_ApiUrl:-}"
Html5Mode="${EX_Html5Mode:-false}"
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

config_header="(function () {
  'use strict';

  angular.module('app.config', [])"

config="
    .constant('BASE_URL', '$ApiUrl' || window.location.origin)
    .constant('EXCEPTIONLESS_API_KEY', '$EX_ExceptionlessApiKey')
    .constant('EXCEPTIONLESS_SERVER_URL', '$EX_ExceptionlessServerUrl')
    .constant('FACEBOOK_APPID', '$FacebookAppId')
    .constant('GITHUB_APPID', '$GitHubAppId')
    .constant('GOOGLE_APPID', '$GoogleAppId')
    .constant('INTERCOM_APPID', '$IntercomAppId')
    .constant('LIVE_APPID', '$MicrosoftAppId')
    .constant('SLACK_APPID', '$SlackAppId')
    .constant('STRIPE_PUBLISHABLE_KEY', '$EX_StripePublishableApiKey')
    .constant('SYSTEM_NOTIFICATION_MESSAGE', '$EX_NotificationMessage')
    .constant('USE_HTML5_MODE', $Html5Mode)
    .constant('USE_SSL', $EnableSsl)
    .constant('ENABLE_ACCOUNT_CREATION', $EnableAccountCreation);"
config_footer="
}());"

echo "Exceptionless UI Config"
echo "$config"

checksum=`echo -n $config | md5sum | cut -c 1-32`
echo "$config_header$config$config_footer" > "app.config.$checksum.js"

CONTENT=$(cat index.html)
echo "$CONTENT" | sed -E "s/app\.config\..+\.js/app.config.$checksum.js/" > index.html
