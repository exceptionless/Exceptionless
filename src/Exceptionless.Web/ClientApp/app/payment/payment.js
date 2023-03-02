(function () {
  'use strict';

  angular.module('app.payment', [
    'ui.router',

    'exceptionless.organization',
    'exceptionless.rate-limit'
  ])
  .config(function ($stateProvider) {
    $stateProvider.state('payment', {
      title: 'View Invoice',
      url: '/payment/:id',
      controller: 'Payment',
      controllerAs: 'vm',
      parent: null,
      templateUrl: 'app/payment/payment.tpl.html'
    });
  });
}());
