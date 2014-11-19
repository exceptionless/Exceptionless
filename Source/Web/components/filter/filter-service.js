(function () {
  'use strict';

  angular.module('exceptionless.filter')
    .factory('filterService', ['$rootScope', 'filterStoreService', function ($rootScope, filterStoreService) {
      var _includeFixed = filterStoreService.getIncludeFixed();
      var _includeHidden = filterStoreService.getIncludeHidden();
      var _organizationId = filterStoreService.getOrganizationId();
      var _projectId = filterStoreService.getProjectId();
      var _rawfilter = filterStoreService.getRawFilter();
      var _timeFilter = filterStoreService.getTimeFilter();

      function apply(source) {
        return angular.extend({}, getDefaultOptions(), source);
      }

      function buildFilter() {
        var filters = [];
        // TODO: This needs to be fixed in the stack repository.
        //filters.push('fixed:'.concat(_includeFixed === true));
        filters.push('hidden:'.concat(_includeHidden === true));

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
        var options = { offset: getTimeZoneOffset() };

        var filter = buildFilter();
        if (filter) {
          angular.extend(options, { filter: filter });
        }

        if (_timeFilter) {
          angular.extend(options, { time: _timeFilter });
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

      function getTime() {
        return _timeFilter;
      }

      function getTimeZoneOffset() {
        return new Date().getTimezoneOffset() * -1;
      }

      function setIncludeFixed(includeFixed) {
        if (angular.equals(includeFixed, _includeFixed)) {
          return;
        }

        _includeFixed = includeFixed === true;
        filterStoreService.setIncludeFixed(_includeFixed, { setHistory: true });
        fireFilterChanged();
      }

      function setIncludeHidden(includeHidden) {
        if (angular.equals(includeHidden, _includeHidden)) {
          return;
        }

        _includeHidden = includeHidden === true;
        filterStoreService.setIncludeHidden(_includeHidden, { setHistory: true });
        fireFilterChanged();
      }

      function setOrganizationId(id) {
        if (angular.equals(id, _organizationId)) {
          return;
        }

        _organizationId = id;
        filterStoreService.setOrganizationId(_organizationId, { setHistory: true });

        _projectId = null;
        filterStoreService.setProjectId(_projectId, { replaceHistory: true });

        fireFilterChanged();
      }

      function setProjectId(id) {
        if (angular.equals(id, _projectId)) {
          return;
        }

        _projectId = id;
        filterStoreService.setProjectId(_projectId, { setHistory: true });
        _organizationId = null;
        filterStoreService.setOrganizationId(_organizationId, { replaceHistory: true });

        fireFilterChanged();
      }

      function setTime(time) {
        if (angular.equals(time, _timeFilter)) {
          return;
        }

        _timeFilter = time ? time : null;
        filterStoreService.setTimeFilter(_timeFilter, { setHistory: true });
        fireFilterChanged();
      }

      function setFilter(filter) {
        if (angular.equals(filter, _rawfilter)) {
          return;
        }

        _rawfilter = filter;
        filterStoreService.setRawFilter(_timeFilter, { setHistory: true });
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
        getTime: getTime,
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
