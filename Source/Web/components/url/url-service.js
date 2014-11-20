(function () {
  'use strict';

  angular.module('exceptionless.url', [])
    .factory('urlService', ['$state', function ($state) {
      function buildFilterUrl(options) {
        var routeParts = [];
        var routeParams = {};

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
        return $state.href('app.' + routeParts.join('-'), routeParams);
      }

      function buildUrl(isSecure, host, port, path, queryString) {
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

        if (Object.keys(queryString).length > 0) {
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
    }
    ]);
}());
