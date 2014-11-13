(function () {
  'use strict';

  angular.module('app')
    .controller('App', ['$window', 'BASE_URL', 'signalRService', function ($window, BASE_URL, signalRService) {
      var vm = this;
      vm.settings = {
        headerFixed: true,
        asideFixed: true,
        asideFolded: false
      };
      vm.user = {
        name: 'Eric J. Smith',
        avatar_url: 'http://www.gravatar.com/avatar/3f307a0eedda99476af09a6568c16c14.png'
      };
      vm.project = {id: '537650f3b77efe23a47914f4'};
      vm.version = '2.0.0';

      function isSmartDevice($window) {
        var ua = $window['navigator']['userAgent'] || $window['navigator']['vendor'] || $window['opera'];
        return (/iPhone|iPod|iPad|Silk|Android|BlackBerry|Opera Mini|IEMobile/).test(ua);
      }

      if (!!navigator.userAgent.match(/MSIE/i))
        angular.element($window.document.body).addClass('ie');

      if (isSmartDevice($window))
        angular.element($window.document.body).addClass('smart');

      // NOTE: we don't dispose of the SignalR timeout because it should never be disposed..
      signalRService.startDelayed(BASE_URL, 'd795c4406f6b4bc6ae8d787c65d0274d');
    }
    ]);
}());
