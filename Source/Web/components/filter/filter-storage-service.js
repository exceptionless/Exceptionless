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

      function getOrganizationId() {
        return _store.get('oid');
      }

      function getProjectId() {
        return _store.get('pid');
      }

      function getRawFilter() {
        return _store.get('raw');
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

      function setOrganizationId(organizationId, options) {
        _store.set('oid', organizationId);
      }

      function setProjectId(projectId, options) {
        _store.set('pid', projectId);
      }

      function setRawFilter(rawFilter, options) {
        _store.set('raw', rawFilter);
      }

      function setTimeFilter(timeFilter, options) {
        _store.set('time', timeFilter);
      }

      var service = {
        getIncludeFixed: getIncludeFixed,
        getIncludeHidden: getIncludeHidden,
        getOrganizationId: getOrganizationId,
        getProjectId: getProjectId,
        getRawFilter: getRawFilter,
        getTimeFilter: getTimeFilter,
        setIncludeFixed: setIncludeFixed,
        setIncludeHidden: setIncludeHidden,
        setOrganizationId: setOrganizationId,
        setProjectId: setProjectId,
        setRawFilter: setRawFilter,
        setTimeFilter: setTimeFilter
      };

      return service;
    }]);
}());
