(function() {
    'use strict';

    angular.module('exceptionless.ui-loading-bar', [])
        .directive('uiLoadingBar', ['$rootScope', '$location', '$anchorScroll', function($rootScope, $location, $anchorScroll) {
            return {
                restrict: 'AC',
                template:'<span class="bar"></span>',
                link: function(scope, el, attrs) {
                    el.addClass('loadingbar hide');
                    scope.$on('$stateChangeStart', function(event) {
                        $location.hash('app');
                        $anchorScroll();
                        el.removeClass('hide').addClass('active');
                    });
                    scope.$on('$stateChangeSuccess', function( event, toState, toParams, fromState ) {
                        event.targetScope.$watch('$viewContentLoaded', function(){
                            el.addClass('hide').removeClass('active');
                        });
                    });
                }
            };
        }]);
}());
