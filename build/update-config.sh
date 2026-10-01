#!/bin/bash
set -euo pipefail

# Only publish client IDs. OAuth secrets must never enter env.js or startup logs.
declare -A oauth_ids=()
IFS=';' read -ra oauth_parts <<< "${EX_ConnectionStrings__OAuth:-}"
for part in "${oauth_parts[@]}"; do
    key="${part%%=*}"
    case "$key" in
        FacebookId|GitHubId|GoogleId|IntercomId|MicrosoftId|SlackId)
            oauth_ids[$key]="${part#*=}" ;;
    esac
done

js_string() {
    local value="${1:-}"
    value="${value//\\/\\\\}"
    value="${value//\'/\\\'}"
    value="${value//$'\r'/\\r}"
    value="${value//$'\n'/\\n}"
    printf "'%s'" "$value"
}

write_setting() {
    printf '    %s: ' "$1"
    js_string "$2"
    printf ',\n'
}

mkdir -p _app
{
    printf 'export const env={\n    PUBLIC_BASE_URL: '
    js_string "${EX_ApiUrl:-}"
    printf ' || window.location.origin,\n'
    write_setting PUBLIC_ENABLE_ACCOUNT_CREATION "${EX_EnableAccountCreation:-true}"
    write_setting PUBLIC_SYSTEM_NOTIFICATION_MESSAGE "${EX_NotificationMessage:-}"
    write_setting PUBLIC_EXCEPTIONLESS_API_KEY "${EX_ExceptionlessApiKey:-}"
    write_setting PUBLIC_EXCEPTIONLESS_CLIENT_SETUP_SHOW_SERVER_URL "${EX_ClientSetupShowServerUrl:-true}"
    write_setting PUBLIC_EXCEPTIONLESS_SERVER_URL "${EX_ExceptionlessServerUrl:-}"
    write_setting PUBLIC_STRIPE_PUBLISHABLE_KEY "${EX_StripePublishableApiKey:-}"
    write_setting PUBLIC_FACEBOOK_APPID "${oauth_ids[FacebookId]:-}"
    write_setting PUBLIC_GITHUB_APPID "${oauth_ids[GitHubId]:-}"
    write_setting PUBLIC_GOOGLE_APPID "${oauth_ids[GoogleId]:-}"
    write_setting PUBLIC_MICROSOFT_APPID "${oauth_ids[MicrosoftId]:-}"
    write_setting PUBLIC_INTERCOM_APPID "${oauth_ids[IntercomId]:-}"
    write_setting PUBLIC_SLACK_APPID "${oauth_ids[SlackId]:-}"
    printf '};\n'
} > _app/env.js

checksum=$(md5sum _app/env.js | cut -c 1-32)
sed -E -i "s|/_app/env.js(\\?v=[a-f0-9]+)?|/_app/env.js?v=$checksum|g" index.html
