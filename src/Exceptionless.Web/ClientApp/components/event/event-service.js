(function () {
  'use strict';

  angular.module('exceptionless.event', [
    'restangular',

    'exceptionless.filter',
    'exceptionless.objectid'
  ])
  .factory('eventService', function (filterService, objectIDService, organizationService, Restangular) {
    function calculateAveragePerHour(total, organizations) {
      var range = filterService.getTimeRange();
      range.start = moment.max([range.start, moment(filterService.getOldestPossibleEventDate()), moment(organizationService.getOldestPossibleEventDate(organizations))].filter(function(d){ return !!d; }));
      range.end = range.end || moment();

      var result = total / range.end.diff(range.start, 'hours', true);
      return !isNaN(parseFloat(result)) && isFinite(result) ? result : 0.0;
    }

    function count(aggregations, includeStatusFilter, optionsCallback) {
      var options = filterService.apply((aggregations && aggregations.length > 0) ? { aggregations: aggregations } : {}, includeStatusFilter);
      options = angular.isFunction(optionsCallback) ? optionsCallback(options) : options;

      var organization = filterService.getOrganizationId();
      if (organization) {
        return Restangular.one('organizations', organization).one('events', 'count').get(options);
      }

      var project = filterService.getProjectId();
      if (project) {
        return Restangular.one('projects', project).one('events', 'count').get(options);
      }

      return Restangular.one('events', 'count').get(options);
    }

    function getAll(options, optionsCallback) {
      optionsCallback = angular.isFunction(optionsCallback) ? optionsCallback : function(o){ return o; };
      var mergedOptions = optionsCallback(filterService.apply(options));

      var organization = filterService.getOrganizationId();
      if (organization) {
        return Restangular.one('organizations', organization).all('events').getList(mergedOptions);
      }

      var project = filterService.getProjectId();
      if (project) {
        return Restangular.one('projects', project).all('events').getList(mergedOptions);
      }

      return Restangular.all('events').getList(mergedOptions);
    }

    function getAllSessions(options, optionsCallback) {
      optionsCallback = angular.isFunction(optionsCallback) ? optionsCallback : function(o){ return o; };
      var mergedOptions = optionsCallback(filterService.apply(options, false));

      var organization = filterService.getOrganizationId();
      if (organization) {
        return Restangular.one('organizations', organization).one('events').all('sessions').getList(mergedOptions);
      }

      var project = filterService.getProjectId();
      if (project) {
        return Restangular.one('projects', project).one('events').all('sessions').getList(mergedOptions);
      }

      return Restangular.one('events').all('sessions').getList(mergedOptions);
    }

    function getById(id, options, optionsCallback) {
      optionsCallback = angular.isFunction(optionsCallback) ? optionsCallback : function(o){ return o; };
      return Restangular.one('events', id).get(optionsCallback(filterService.apply(options)));
    }

    function getByReferenceId(id, options) {
      return Restangular.one('events', 'by-ref').all(id).getList(filterService.apply(options, false));
    }

    function getBySessionId(projectId, id, options, optionsCallback) {
      optionsCallback = angular.isFunction(optionsCallback) ? optionsCallback : function(o){ return o; };
      return Restangular.one('projects', projectId).one('events', 'sessions').all(id).getList(optionsCallback(filterService.apply(options, false)));
    }

    function getByStackId(id, options) {
      return Restangular.one('stacks', id).all('events').getList(filterService.apply(options, false));
    }

    function markCritical(id) {
      return Restangular.one('events', id).one('mark-critical').post();
    }

    function markNotCritical(id) {
      return Restangular.one('events', id).one('mark-critical').remove();
    }

    function remove(id) {
      return Restangular.one('events', id).remove();
    }

    var service = {
      calculateAveragePerHour: calculateAveragePerHour,
      count: count,
      getAll: getAll,
      getAllSessions: getAllSessions,
      getById: getById,
      getByReferenceId: getByReferenceId,
      getBySessionId: getBySessionId,
      getByStackId: getByStackId,
      markCritical: markCritical,
      markNotCritical: markNotCritical,
      remove: remove
    };
    return service;
  });
}());
