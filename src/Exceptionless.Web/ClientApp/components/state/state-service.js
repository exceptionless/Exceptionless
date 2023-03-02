(function () {
  'use strict';

  angular.module('exceptionless.state', [
    'angular-locker',
    'ui.router'
  ])
  .factory('stateService', function ($location, $state, locker) {
    var _store = locker.driver('session').namespace('state');
    function clear() {
      _store.forget(['name', 'params']);
    }

    function restore(secondaryStateNameToRedirect, secondaryStateParams) {
      var name = _store.pull('name');
      var params = _store.pull('params') || {};

      if (name) {
        return $state.go(name, params);
      }

      return secondaryStateNameToRedirect ? $state.go(secondaryStateNameToRedirect, secondaryStateParams || {}) : $location.path('/');
    }

    function save(exclusions) {
      if (exclusions && exclusions.filter(function (e) { return $state.current.name.startsWith(e); })[0]) {
        return;
      }

      if (_store.has('name')) {
        return;
      }

      _store.put('name', $state.current.name);
      _store.put('params', $state.params);
    }

    function saveRequested(exclusions, requestedStateName, requestedStateParams) {
      save(exclusions);

      if (!requestedStateName) {
        return;
      }

      if (_store.get('name', false)) {
        return;
      }

      _store.put('name', requestedStateName);
      _store.put('params', requestedStateParams);
    }

    var service = {
      clear: clear,
      restore: restore,
      saveRequested: saveRequested,
      save: save
    };

    return service;
  });
}());
