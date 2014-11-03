(function () {
    'use strict';

    angular.module('exceptionless.filter', [])
        .factory('filterService', [function () {
            function apply(source) {
                return angular.extend({}, getDefaultOptions(), source);
            }

            function getDefaultOptions() {
                return {
                    //filter: 'tag:Europe',
                    time: 'last 30 days',
                    offset: getTimeZoneOffset()
                };
            }

            function getTimeZoneOffset() {
                return new Date().getTimezoneOffset() * -1;
            }

            var service = {
                apply: apply,
                getDefaultOptions: getDefaultOptions
            };

            return service;
        }
    ]);
}());
