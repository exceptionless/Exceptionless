(function () {
  'use strict';

  angular.module('app.config', [])
    .constant('BASE_URL', 'https://localhost:5001')
    .constant('EXCEPTIONLESS_API_KEY')
    .constant('EXCEPTIONLESS_SERVER_URL')
    .constant('FACEBOOK_APPID')
    .constant('GITHUB_APPID')
    .constant('GOOGLE_APPID')
    .constant('INTERCOM_APPID')
    .constant('LIVE_APPID')
    .constant('SLACK_APPID')
    .constant('STRIPE_PUBLISHABLE_KEY')
    .constant('SYSTEM_NOTIFICATION_MESSAGE')
    .constant('USE_HTML5_MODE', false)
    .constant('USE_SSL', false)
    .constant('ENABLE_ACCOUNT_CREATION', true);
}());
