(function () {
    'use strict';

    angular.module('app')
        .controller('App', ['$window', 'BASE_URL', 'signalRService', function ($window, BASE_URL, signalRService) {
            var vm = this;
            vm.settings = {
                headerFixed: true,
                asideFixed: false,
                asideFolded: false
            };
            vm.user = {
                name: 'Blake Niemyjski',
                avatar_url: 'https://secure.gravatar.com/avatar/89b10deee628535a5510db131f983541?s=55&amp;d=identicon&amp;r=PG'
            };
            vm.project = { id: '537650f3b77efe23a47914f4' };
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
