(function () {
  'use strict';

  angular.module('exceptionless.filter')
    .factory('filterStoreService', ['$window', 'store', function ($window, store) {
      var _store = store.getNamespacedStore('filter');

      function getIncludeFixed() {
        return _store.get('fixed');
      }

      function getIncludeHidden() {
        return _store.get('hidden');
      }

      function getTimeFilter() {
        return _store.get('time');
      }

      function setIncludeFixed(includeFixed, options) {
        _store.set('fixed', includeFixed);
      }

      function setIncludeHidden(includeHidden, options) {
        _store.set('hidden', includeHidden);
      }

      function setTimeFilter(timeFilter, options) {
        _store.set('time', timeFilter);
      }

      var service = {
        getIncludeFixed: getIncludeFixed,
        getIncludeHidden: getIncludeHidden,
        getTimeFilter: getTimeFilter,
        setIncludeFixed: setIncludeFixed,
        setIncludeHidden: setIncludeHidden,
        setTimeFilter: setTimeFilter
      };

      return service;
    }]);
}());
