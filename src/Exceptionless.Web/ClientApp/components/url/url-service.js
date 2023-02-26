(function () {
  'use strict';

  angular.module('exceptionless.url', [])
    .factory('urlService', function ($state) {
      function buildFilterUrl(options, routeParameters) {
        var routeParts = [];
        var routeParams = {};

        if (options.routePrefix) {
          routeParts.push(options.routePrefix);
        }

        if (options.organizationId) {
          routeParts.push('organization');
          routeParams.organizationId = options.organizationId;
        } else if (options.projectId) {
          routeParts.push('project');
          routeParams.projectId = options.projectId;
        }

        if (options.type) {
          routeParts.push('type');
          routeParams.type = options.type;
        }

        routeParts.push(options.route);

        var moduleName = options.moduleName || 'app';
        var parameters = angular.extend({}, routeParameters || {}, routeParams);
        return $state.href(moduleName + '.' + routeParts.join('-'), parameters, { absolute: true });
      }

      function buildUrl(isSecure, host, port, path, queryString) {
        if (!host) {
          return null;
        }

        var url = (isSecure ? 'https://' : 'http://') + host;

        if (port !== 80 && port !== 443) {
          url += ':' + port;
        }

        if (path) {
          if (path && path.indexOf('/') !== 0) {
            url += '/';
          }

          url += path;
        }

        if (!!queryString && Object.keys(queryString).length > 0) {
          var isFirst = true;
          for (var key in queryString) {
            if (isFirst) {
              url += '?';
              isFirst = false;
            } else {
              url += '&';
            }

            url += key + '=' + queryString[key];
          }
        }

        return url;
      }

      var service = {
        buildFilterUrl: buildFilterUrl,
        buildUrl: buildUrl
      };

      return service;
    });
}());
