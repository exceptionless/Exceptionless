(function () {
  'use strict';

  angular.module('exceptionless.filter')
    .factory('filterService', ['$rootScope', function ($rootScope) {
      var _rawfilter;
      var _organizationFilter;
      var _projectFilter;
      var _timeFilter;

      function apply(source) {
        return angular.extend({}, getDefaultOptions(), source);
      }

      function buildFilter() {
        var filters = [];
        if (_organizationFilter)
          filters.push('organization:' + _organizationFilter);

        if (_projectFilter)
          filters.push('project:' + _projectFilter);

        if (_rawfilter)
          filters.push(_rawfilter);

        return filters.join(' ');
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

      function getProjectId() {
        return _projectFilter;
      }

      function getOrganizationId() {
        return _organizationFilter;
      }

      function getTimeZoneOffset() {
        return new Date().getTimezoneOffset() * -1;
      }

      function clearOrganizationAndProjectFilter() {
        if (!_organizationFilter && !_projectFilter) {
          return;
        }

        _organizationFilter = _projectFilter = null;
        fireFilterChanged();
      }

      function setOrganizationId(id) {
        if (angular.equals(id, _organizationFilter)) {
          return;
        }

        _organizationFilter = id;
        _projectFilter = null;
        fireFilterChanged();
      }

      function setProjectId(id) {
        if (angular.equals(id, _projectFilter)) {
          return;
        }

        _projectFilter = id;
        _organizationFilter = null;
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
        clearOrganizationAndProjectFilter: clearOrganizationAndProjectFilter,
        getProjectId: getProjectId,
        getOrganizationId: getOrganizationId,
        setFilter: setFilter,
        setOrganizationId: setOrganizationId,
        setProjectId: setProjectId,
        setTime: setTime
      };

      return service;
    }
    ]);
}());
