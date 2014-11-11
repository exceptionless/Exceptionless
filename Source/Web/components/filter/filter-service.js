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

      function getTimeZoneOffset() {
        return new Date().getTimezoneOffset() * -1;
      }

      function setOrganization(id) {
        if (angular.equals(id, _organizationFilter)) {
          return;
        }

        if (id) {
          _organizationFilter = id;
          _projectFilter = void 0;
        } else {
          _organizationFilter = void 0;
        }
        fireFilterChanged();
      }

      function setProject(id) {
        if (angular.equals(id, _projectFilter)) {
          return;
        }

        if (id) {
          _projectFilter = id;
          _organizationFilter = void 0;
        } else {
          _projectFilter = void 0;
        }

        fireFilterChanged();
      }

      function setTime(time) {
        if (angular.equals(time, _timeFilter)) {
          return;
        }

        console.log(time);
        _timeFilter = time ? time : void 0;
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
        setFilter: setFilter,
        setOrganization: setOrganization,
        setProject: setProject,
        setTime: setTime
      };

      return service;
    }
    ]);
}());
