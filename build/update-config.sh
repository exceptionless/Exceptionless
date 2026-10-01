#!/bin/bash
set -euo pipefail

# Publish client IDs only; OAuth secrets must not enter client configuration or logs.
IFS=';' read -ra oauthParts <<< "${EX_ConnectionStrings__OAuth:-}"
for part in "${oauthParts[@]}"; do
    key="${part%%=*}"
    key="${key//[[:space:]]/}"
    value="${part#*=}"
    value="${value#"${value%%[![:space:]]*}"}"
    value="${value%"${value##*[![:space:]]}"}"
    case "$key" in
        FacebookId) FacebookAppId="$value" ;;
        GitHubId) GitHubAppId="$value" ;;
        GoogleId) GoogleAppId="$value" ;;
        IntercomId) IntercomAppId="$value" ;;
        MicrosoftId) MicrosoftAppId="$value" ;;
        SlackId) SlackAppId="$value" ;;
    esac
done

write_setting() {
    local value="${2:-}"
    value="${value//\\/\\\\}"
    value="${value//\'/\\\'}"
    value="${value//$'\r'/\\r}"
    value="${value//$'\n'/\\n}"
    printf "    %s: '%s',\n" "$1" "$value"
}

mkdir -p _app
{
    printf 'export const env={\n'
    write_setting PUBLIC_BASE_URL "${EX_ApiUrl:-}"
    write_setting PUBLIC_ENABLE_ACCOUNT_CREATION "${EX_EnableAccountCreation:-true}"
    write_setting PUBLIC_SYSTEM_NOTIFICATION_MESSAGE "${EX_NotificationMessage:-}"
    write_setting PUBLIC_EXCEPTIONLESS_API_KEY "${EX_ExceptionlessApiKey:-}"
    write_setting PUBLIC_EXCEPTIONLESS_CLIENT_SETUP_SHOW_SERVER_URL "${EX_ClientSetupShowServerUrl:-true}"
    write_setting PUBLIC_EXCEPTIONLESS_SERVER_URL "${EX_ExceptionlessServerUrl:-}"
    write_setting PUBLIC_STRIPE_PUBLISHABLE_KEY "${EX_StripePublishableApiKey:-}"
    write_setting PUBLIC_FACEBOOK_APPID "${FacebookAppId:-}"
    write_setting PUBLIC_GITHUB_APPID "${GitHubAppId:-}"
    write_setting PUBLIC_GOOGLE_APPID "${GoogleAppId:-}"
    write_setting PUBLIC_MICROSOFT_APPID "${MicrosoftAppId:-}"
    write_setting PUBLIC_INTERCOM_APPID "${IntercomAppId:-}"
    write_setting PUBLIC_SLACK_APPID "${SlackAppId:-}"
    printf '};\n'
    printf 'env.PUBLIC_BASE_URL ||= window.location.origin;\n'
} > _app/env.js

checksum=$(md5sum _app/env.js | cut -c 1-32)
sed -E -i "s|/_app/env.js(\\?v=[a-f0-9]+)?|/_app/env.js?v=$checksum|g" index.html
