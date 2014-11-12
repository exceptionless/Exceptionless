(function () {
  'use strict';

  angular.module('exceptionless.filter')
    .factory('filterService', ['$rootScope', function ($rootScope) {
      var _includeFixed;
      var _includeHidden;
      var _organizationId;
      var _projectId;
      var _rawfilter;
      var _timeFilter;

      function apply(source) {
        return angular.extend({}, getDefaultOptions(), source);
      }

      function buildFilter() {
        var filters = [];
        if (_includeFixed) {
          filters.push('fixed:' + _includeFixed === true);
        }

        if (_includeHidden) {
          filters.push('hidden:' + _includeHidden === true);
        }

        if (_organizationId) {
          filters.push('organization:' + _organizationId);
        }

        if (_projectId) {
          filters.push('project:' + _projectId);
        }

        if (_rawfilter) {
          filters.push(_rawfilter);
        }

        return filters.join(' ');
      }

      function clearFilterAndIncludeFixedAndIncludeHidden() {
        if (!_rawfilter && !_includeFixed && !_includeHidden) {
          return;
        }

        _rawfilter = _includeFixed = _includeHidden = null;
        fireFilterChanged();
      }

      function clearOrganizationAndProjectFilter() {
        if (!_organizationId && !_projectId) {
          return;
        }

        _organizationId = _projectId = null;
        fireFilterChanged();
      }

      function fireFilterChanged() {
        $rootScope.$emit('filterChanged', getDefaultOptions());
      }

      function getDefaultOptions() {
        var options = {offset: getTimeZoneOffset()};

        var filter = buildFilter();
        if (filter) {
          angular.extend(options, {filter: filter});
        }

        if (_timeFilter) {
          angular.extend(options, {time: _timeFilter});
        }

        return options;
      }

      function getFilter() {
        return _rawfilter;
      }

      function getIncludeFixed() {
        return _includeFixed === true;
      }

      function getIncludeHidden() {
        return _includeHidden === true;
      }

      function getProjectId() {
        return _projectId;
      }

      function getOrganizationId() {
        return _organizationId;
      }

      function getTimeZoneOffset() {
        return new Date().getTimezoneOffset() * -1;
      }

      function setIncludeFixed(includeFixed) {
        if (angular.equals(includeFixed, _includeFixed)) {
          return;
        }

        _includeFixed = includeFixed === true;
        fireFilterChanged();
      }

      function setIncludeHidden(includeHidden) {
        if (angular.equals(includeHidden, _includeHidden)) {
          return;
        }

        _includeHidden = includeHidden === true;
        fireFilterChanged();
      }

      function setOrganizationId(id) {
        if (angular.equals(id, _organizationId)) {
          return;
        }

        _organizationId = id;
        _projectId = null;
        fireFilterChanged();
      }

      function setProjectId(id) {
        if (angular.equals(id, _projectId)) {
          return;
        }

        _projectId = id;
        _organizationId = null;
        fireFilterChanged();
      }

      function setTime(time) {
        if (angular.equals(time, _timeFilter)) {
          return;
        }

        _timeFilter = time ? time : null;
        fireFilterChanged();
      }

      function setFilter(filter) {
        if (angular.equals(filter, _rawfilter)) {
          return;
        }

        _rawfilter = filter;
        fireFilterChanged();
      }

      var service = {
        apply: apply,
        clearFilterAndIncludeFixedAndIncludeHidden: clearFilterAndIncludeFixedAndIncludeHidden,
        clearOrganizationAndProjectFilter: clearOrganizationAndProjectFilter,
        getFilter: getFilter,
        getIncludeFixed: getIncludeFixed,
        getIncludeHidden: getIncludeHidden,
        getProjectId: getProjectId,
        getOrganizationId: getOrganizationId,
        setFilter: setFilter,
        setIncludeFixed: setIncludeFixed,
        setIncludeHidden: setIncludeHidden,
        setOrganizationId: setOrganizationId,
        setProjectId: setProjectId,
        setTime: setTime
      };

      return service;
    }]);
}());
