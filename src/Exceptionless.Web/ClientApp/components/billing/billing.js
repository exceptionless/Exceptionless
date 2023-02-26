(function () {
  'use strict';

  angular.module('exceptionless.billing', [
    'angularPayments',
    'angular-stripe',
    'ui.bootstrap',

    'dialogs.main',

    'app.config',
    'exceptionless',
    'exceptionless.admin',
    'exceptionless.analytics',
    'exceptionless.autofocus',
    'exceptionless.dialog',
    'exceptionless.notification',
    'exceptionless.organization',
    'exceptionless.promise-button',
    'exceptionless.user',
    'exceptionless.refresh'
  ]);
}());
