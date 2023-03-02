(function() {
    'use strict';
    angular
        .module("exceptionless.translate", [
            'pascalprecht.translate'
        ])
        .factory("translateService", function($translate) {
            var T = {
                T: function(key, params, interpolation, uses, strategy) {
                    if (key) {
                        return $translate.instant(key, params, interpolation, uses, strategy);
                    }
                    return key;
                }
            };
            return T;
        });
}());