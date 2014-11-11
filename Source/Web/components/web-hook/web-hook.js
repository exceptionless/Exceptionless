(function () {
  'use strict';

  angular.module('exceptionless.web-hook', [
    'checklist-model',
    'restangular',

    // Custom dialog dependencies
    'ui.bootstrap',
    'dialogs.main',
    'dialogs.default-translations'
  ]);
}());
