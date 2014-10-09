(function () {
    'use strict';

    angular.module('app.project')
        .directive('configurationKeyUniqueValidator', ['$q', function ($q) {
            return {
                restrict: 'A',
                require: 'ngModel',
                link: function (scope, element, attrs, ngModel) {
                    ngModel.$validators.configurationKeyUnique = function (key) {
                        var found = false;
                        angular.forEach(scope.configuration, function(v, k) {
                            if (key === k) {
                                found = true;
                            }
                        });

                        var deferred = $q.defer();
                        if (found){
                            deferred.resolve(true);
                        } else {
                            deferred.reject(false)
                        }

                        return deferred.promise;
                    }
                }
            }
        }]);
}());
