(function () {
  'use strict';

  angular.module('exceptionless.filter')
    .factory('filterService', function ($rootScope, dateRangeParserService, filterStoreService, objectIDService, organizationService) {
      var DEFAULT_TIME_FILTER = 'last week';
      var _time = filterStoreService.getTimeFilter() || DEFAULT_TIME_FILTER;
      var _eventType, _organizationId, _projectId, _raw, _status;

      function apply(source, includeStatusFilter) {
        return angular.extend({}, getDefaultOptions(includeStatusFilter), source);
      }

      function buildFilter(includeStatusFilter) {
        includeStatusFilter = (typeof includeStatusFilter !== 'undefined') ?  includeStatusFilter : true;
        var filters = [];

        if (_organizationId) {
          filters.push('organization:' + _organizationId);
        }

        if (_projectId) {
          filters.push('project:' + _projectId);
        }

        if (_eventType) {
          filters.push('type:' + _eventType);
        }

        if (_status) {
          filters.push('status:' + _status);
        }

        var filter = _raw || '';
        var isWildCardFilter = filter.trim() === '*';
        if (includeStatusFilter && !isWildCardFilter && !_status) {
          var hasStatus = filter.search(/\bstatus:/i) !== -1;
          if (!hasStatus) {
            filters.push('(status:open OR status:regressed)');
          }
        }

        if (!!filter && !isWildCardFilter) {
          filters.push('(' + filter + ')');
        }

        return filters.join(' ').trim();
      }

      function clearFilter() {
        if (!_raw) {
          return;
        }

        setFilter(null, false);
        fireFilterChanged();
      }

      function clearOrganizationAndProjectFilter() {
        if (!_organizationId && !_projectId) {
          return;
        }

        _organizationId = _projectId = null;
        fireFilterChanged();
      }

      function fireFilterChanged(includeStatusFilter) {
        var options = {
          organization_id: _organizationId,
          project_id: _projectId,
          type: _eventType,
          status: _status
        };

        $rootScope.$emit('filterChanged', angular.extend(options, getDefaultOptions(includeStatusFilter)));
      }

      function getDefaultOptions(includeStatusFilter) {
        var options = {};

        var offset = getTimeOffset();
        if (offset) {
          angular.extend(options, { offset: offset });
        }

        var filter = buildFilter(includeStatusFilter);
        if (filter) {
          angular.extend(options, { filter: filter });
        }

        if (!!_time && _time !== 'all') {
          angular.extend(options, { time: _time });
        }

        return options;
      }

      function getFilter() {
        return _raw;
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

      function getStatus() {
        return _status;
      }

      function getOldestPossibleEventDate() {
        var date = objectIDService.getDate(getOrganizationId() || getProjectId());
        return date ? moment(date).subtract(3, 'days').toDate() : new Date(2012, 1, 1);
      }

      function getTime() {
        return _time || DEFAULT_TIME_FILTER;
      }

      function getTimeRange() {
        var time = getTime();
        if (time === 'all') {
          return { start: undefined, end: undefined };
        }

        if (time === 'last hour') {
          return { start: moment().subtract(1, 'hours'), end: undefined };
        }

        if (time === 'last 24 hours') {
          return { start: moment().subtract(24, 'hours'), end: undefined };
        }

        if (time === 'last week') {
          return { start: moment().subtract(7, 'days').startOf('day'), end: undefined };
        }

        if (time === 'last 30 days') {
          return { start: moment().subtract(30, 'days').startOf('day'), end: undefined };
        }

        var range = dateRangeParserService.parse(time);
        if (range && range.start && range.end) {
          return { start: moment(range.start), end: moment(range.end) };
        }

        return { start: moment().subtract(7, 'days').startOf('day'), end: undefined };
      }

      function getTimeOffset() {
        var offset = new Date().getTimezoneOffset();
        return offset !== 0 ? offset * -1 + 'm' : undefined;
      }

      function hasFilter() {
        return _raw || (_time && _time !== 'all');
      }

      function includedInProjectOrOrganizationFilter(data) {
        if (!data.organizationId && !data.projectId) {
          return false;
        }

        // The all filter is set.
        if (!_organizationId && !_projectId) {
          return true;
        }

        return _organizationId === data.organizationId || _projectId === data.projectId;
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

      function setStatus(status, suspendNotifications) {
        if (angular.equals(status, _status)) {
          return;
        }

        _status = status;

        if (!suspendNotifications) {
          fireFilterChanged();
        }
      }

      function setOrganizationId(id, suspendNotifications) {
        if (angular.equals(id, _organizationId) || (id && !objectIDService.isValid(id))) {
          return;
        }

        _organizationId = id;
        _projectId = null;

        if (!suspendNotifications) {
          fireFilterChanged();
        }
      }

      function setProjectId(id, suspendNotifications) {
        if (angular.equals(id, _projectId) || (id && !objectIDService.isValid(id))) {
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

        _time = time || DEFAULT_TIME_FILTER;
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

      $rootScope.$on('OrganizationChanged', function ($event, organizationChanged) {
        if (organizationChanged.id === getOrganizationId() &&  organizationChanged.deleted) {
          setOrganizationId();
        }
      });

      $rootScope.$on('ProjectChanged', function ($event, projectChanged) {
        if (projectChanged.id === getProjectId() &&  projectChanged.deleted) {
          setProjectId();
        }
      });

      var service = {
        apply: apply,
        clearFilter: clearFilter,
        clearOrganizationAndProjectFilter: clearOrganizationAndProjectFilter,
        fireFilterChanged: fireFilterChanged,
        getEventType: getEventType,
        getFilter: getFilter,
        getProjectId: getProjectId,
        getOrganizationId: getOrganizationId,
        getOldestPossibleEventDate: getOldestPossibleEventDate,
        getStatus: getStatus,
        getTime: getTime,
        getTimeRange: getTimeRange,
        getTimeOffset: getTimeOffset,
        hasFilter: hasFilter,
        includedInProjectOrOrganizationFilter: includedInProjectOrOrganizationFilter,
        setEventType: setEventType,
        setFilter: setFilter,
        setOrganizationId: setOrganizationId,
        setProjectId: setProjectId,
        setStatus: setStatus,
        setTime: setTime
      };

      return service;
    });
}());
