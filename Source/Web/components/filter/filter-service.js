(function () {
  'use strict';

  angular.module('exceptionless.filter')
    .factory('filterService', ['$rootScope', 'filterStoreService', function ($rootScope, filterStoreService) {
      var _includeFixed = filterStoreService.getIncludeFixed();
      var _includeHidden = filterStoreService.getIncludeHidden();
      var _time = filterStoreService.getTimeFilter();
      var _eventType, _organizationId, _projectId, _raw;

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

        if (_eventType) {
          filters.push('type:' + _eventType);
        }

        if (_raw) {
          filters.push(_raw);
        }

        return filters.join(' ');
      }

      function clearFilterAndIncludeFixedAndIncludeHidden() {
        if (!_raw && !_includeFixed && !_includeHidden) {
          return;
        }

        _raw = _includeFixed = _includeHidden = null;
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

        if (_time) {
          angular.extend(options, { time: _time });
        }

        return options;
      }

      function getFilter() {
        return _raw;
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

      function getEventType() {
        return _eventType;
      }

      function getTime() {
        return _time;
      }

      function getTimeZoneOffset() {
        return new Date().getTimezoneOffset() * -1;
      }

      function setEventType(eventType, suspendNotifications) {
        if (angular.equals(eventType, _eventType)) {
          return;
        }

        _eventType = eventType;

        if (!suspendNotifications) {
          fireFilterChanged();
        }
      }

      function setIncludeFixed(includeFixed, suspendNotifications) {
        if (angular.equals(includeFixed, _includeFixed)) {
          return;
        }

        _includeFixed = includeFixed === true;
        filterStoreService.setIncludeFixed(_includeFixed);

        if (!suspendNotifications) {
          fireFilterChanged();
        }
      }

      function setIncludeHidden(includeHidden, suspendNotifications) {
        if (angular.equals(includeHidden, _includeHidden)) {
          return;
        }

        _includeHidden = includeHidden === true;
        filterStoreService.setIncludeHidden(_includeHidden);

        if (!suspendNotifications) {
          fireFilterChanged();
        }
      }

      function setOrganizationId(id, suspendNotifications) {
        if (angular.equals(id, _organizationId)) {
          return;
        }

        _organizationId = id;
        _projectId = null;

        if (!suspendNotifications) {
          fireFilterChanged();
        }
      }

      function setProjectId(id, suspendNotifications) {
        if (angular.equals(id, _projectId)) {
          return;
        }

        _projectId = id;
        _organizationId = null;

        if (!suspendNotifications) {
          fireFilterChanged();
        }
      }

      function setTime(time, suspendNotifications) {
        if (angular.equals(time, _time)) {
          return;
        }

        _time = time ? time : null;
        filterStoreService.setTimeFilter(_time);

        if (!suspendNotifications) {
          fireFilterChanged();
        }
      }

      function setFilter(raw, suspendNotifications) {
        if (angular.equals(raw, _raw)) {
          return;
        }

        _raw = raw;

        if (!suspendNotifications) {
          fireFilterChanged();
        }
      }

      var service = {
        apply: apply,
        clearFilterAndIncludeFixedAndIncludeHidden: clearFilterAndIncludeFixedAndIncludeHidden,
        clearOrganizationAndProjectFilter: clearOrganizationAndProjectFilter,
        getEventType: getEventType,
        getFilter: getFilter,
        getIncludeFixed: getIncludeFixed,
        getIncludeHidden: getIncludeHidden,
        getProjectId: getProjectId,
        getOrganizationId: getOrganizationId,
        getTime: getTime,
        setEventType: setEventType,
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
