(function () {
  'use strict';

  angular.module('exceptionless.log-level', [
    'ui.bootstrap',

    'exceptionless.notification',
    'exceptionless.organization',
    'exceptionless.project',
    'exceptionless.refresh',
    'exceptionless.translate'
  ])
  .directive('logLevel', [function () {
    return {
      restrict: 'E',
      replace: true,
      scope: {
        projectId: '=',
        source: '='
      },
      templateUrl: 'components/log-level/log-level-directive.tpl.html',
      controller: function ($scope, notificationService, projectService, translateService) {
        var vm = this;

        function get() {
          function onSuccess(response) {
            var configSettings = response.data.plain().settings;
            vm.level = getSourceLogLevel(configSettings, vm.source);
            vm.defaultLevel = getDefaultLogLevel(configSettings, vm.source);
            vm.loading = false;
          }

          function onFailure() {
            notificationService.error(translateService.T('An error occurred while loading the projects.'));
          }

          return projectService.getConfig(vm.projectId).then(onSuccess, onFailure);
        }

        function canRefresh(data) {
          if (!data || !data.type || !vm.projectId) {
            return true;
          }

          return data.type === 'Project' && (!data.id || data.id === vm.projectId);
        }

        function setLogLevel(level) {
          function onSuccess() {
            vm.level = level;
          }

          function onFailure() {
            notificationService.error(translateService.T('An error occurred while saving the configuration setting.'));
          }

          if (vm.loading) {
            return;
          }

          return projectService.setConfig(vm.projectId, '@@log:' + vm.source, level).then(onSuccess, onFailure);
        }

        function setDefaultLogLevel() {
          function onSuccess() {
            vm.level = null;
          }

          function onFailure() {
            notificationService.error(translateService.T('An error occurred while trying to delete the configuration setting.'));
          }

          if (vm.loading) {
            return;
          }

          return projectService.removeConfig(vm.projectId, '@@log:' + vm.source).then(onSuccess, onFailure);
        }

        function getLogLevel(level) {
          switch ((level || '').toLowerCase().trim()) {
            case 'trace':
            case 'true':
            case '1':
            case 'yes':
              return 'trace';
            case 'debug':
              return 'debug';
            case 'info':
              return 'info';
            case 'warn':
              return 'warn';
            case 'error':
              return 'error';
            case 'fatal':
              return 'fatal';
            case 'off':
            case 'false':
            case '0':
            case 'no':
              return 'off';
            default:
              return null;
          }
        }

        function getSourceLogLevel(configSettings, source) {
          if (!configSettings) {
            configSettings = {};
          }

          if (source === undefined) {
            source = '';
          }

          return getLogLevel(configSettings['@@log:' + source]);
        }

        function getDefaultLogLevel(configSettings, source) {
          if (!configSettings) {
            configSettings = {};
          }

          if (source === undefined) {
            source = '';
          }

          var sourcePrefix = '@@log:';
          // sort object keys longest first, then alphabetically.
          var sortedKeys  = Object.keys(configSettings).sort(function(a, b) {
            return  b.length - a.length || a.localeCompare(b);
          });

          for (var index in sortedKeys) {
            var key = sortedKeys[index];
            if (!startsWith(key.toLowerCase(), sourcePrefix)) {
              continue;
            }

            var cleanKey = key.substring(sourcePrefix.length);
            if (cleanKey.toLowerCase() === vm.source.toLowerCase()) {
              continue;
            }

            // check for wildcard match
            if (isMatch(source, [cleanKey])) {
              return getLogLevel(configSettings[key]);
            }
          }

          return getLogLevel(null);
        }

        function isMatch(input, patterns, ignoreCase) {
          if (typeof input !== 'string') {
            return false;
          }

          if (ignoreCase === undefined) {
            ignoreCase = true;
          }

          var trim = /^[\s\uFEFF\xA0]+|[\s\uFEFF\xA0]+$/g;
          input = (ignoreCase ? input.toLowerCase() : input).replace(trim, '');

          return (patterns || []).some(function(pattern) {
            if (typeof pattern !== 'string') {
              return false;
            }

            pattern = (ignoreCase ? pattern.toLowerCase() : pattern).replace(trim, '');
            if (pattern.length <= 0) {
              return false;
            }

            var startsWithWildcard = pattern[0] === '*';
            if (startsWithWildcard) {
              pattern = pattern.slice(1);
            }

            var endsWithWildcard = pattern[pattern.length - 1] === '*';
            if (endsWithWildcard) {
              pattern = pattern.substring(0, pattern.length - 1);
            }

            if (startsWithWildcard && endsWithWildcard) {
              return pattern.length <= input.length && input.indexOf(pattern, 0) !== -1;
            }

            if (startsWithWildcard) {
              return endsWith(input, pattern);
            }

            if (endsWithWildcard) {
              return startsWith(input, pattern);
            }

            return input === pattern;
          });
        }

        function startsWith(input, prefix) {
          return input.substring(0, prefix.length) === prefix;
        }

        function endsWith(input, suffix) {
          return input.indexOf(suffix, input.length - suffix.length) !== -1;
        }

        this.$onInit = function $onInit() {
          vm.canRefresh = canRefresh;
          vm.loading = true;
          vm.level = null;
          vm.defaultLevel = null;
          vm.get = get;
          vm.projectId = $scope.projectId;
          vm.setLogLevel = setLogLevel;
          vm.setDefaultLogLevel = setDefaultLogLevel;
          vm.source = $scope.source || '';

          $scope.$watch('projectId', function(projectId) {
            if (projectId) {
              vm.projectId = projectId;
              return get();
            }
          });
        };
      },
      controllerAs: 'vm'
    };
  }]);
}());
