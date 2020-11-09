(function () {
  'use strict';

  angular.module('exceptionless.web-hook', [
    'checklist-model',
    'ngMessages',
    'restangular',
    'ui.bootstrap',

    'dialogs.main',

    'exceptionless',
    'exceptionless.autofocus'
  ]);
}());
