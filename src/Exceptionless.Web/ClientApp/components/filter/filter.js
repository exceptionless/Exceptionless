(function () {
  'use strict';

  angular.module('exceptionless.filter', [
    'angular-locker',

    'exceptionless.date-range-parser',
    'exceptionless.objectid',
    'exceptionless.organization'
  ]);
}());
