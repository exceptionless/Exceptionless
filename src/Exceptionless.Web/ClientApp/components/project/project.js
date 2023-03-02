(function () {
  'use strict';

  angular.module('exceptionless.project', [
      'restangular',
      'satellizer',

      'app.config'
    ])
    .config(function ($authProvider, SLACK_APPID) {
      if (SLACK_APPID) {
        $authProvider.oauth2({
          name: 'slack',
          authorizationEndpoint: 'https://slack.com/oauth/authorize',
          clientId: SLACK_APPID,
          redirectUri: window.location.origin,
          requiredUrlParams: ['client_id', 'scope'],
          optionalUrlParams: ['redirect_uri', 'state', 'team'],
          scope: ['incoming-webhook'],
          scopeDelimiter: ' ',
          display: 'popup',
          popupOptions: { width: 580, height: 630 },
          state: function () { return encodeURIComponent(Math.random().toString(36).substr(2)); }
        });
      }
    });
}());
