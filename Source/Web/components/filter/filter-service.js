(function () {
    'use strict';

    angular.module('exceptionless.filter')
        .factory('filterService', ['$rootScope', function ($rootScope) {
            var _filter = null;

            function apply(source) {
                return angular.extend({}, getDefaultOptions(), source);
            }

            function fireFilterChanged() {
                $rootScope.$emit('filterChanged', getDefaultOptions());
            }

            function getDefaultOptions() {
                return {
                    filter: _filter,
                    time: 'last 30 days',
                    offset: getTimeZoneOffset()
                };
            }

            function getTimeZoneOffset() {
                return new Date().getTimezoneOffset() * -1;
            }

            function setFilter(filter) {
                _filter = filter;
                fireFilterChanged();
            }

            var service = {
                apply: apply,
                getDefaultOptions: getDefaultOptions,
                setFilter: setFilter
            };

            return service;
        }
    ]);
}());
