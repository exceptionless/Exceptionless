(function () {
  'use strict';

  angular.module('app.config', [])
    .constant('BASE_URL', 'http://localhost:50000')
    .constant('VERSION', '2.0.0');
}());
