(function () {
  'use strict';

  angular.module('app.config', [])
    .constant('BASE_URL', 'https://api-master.exceptionless.com')
    .constant('VERSION', '2.0.0');
}());
