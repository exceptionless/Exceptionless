/* global ObjectId:false */

(function () {
  'use strict';

  angular.module('exceptionless.objectid', [])
  .factory('objectIDService', function () {

    function create(id) {
      return new ObjectId(id);
    }

    function isValid(id) {
      if (!id || !(typeof id === 'number' || id instanceof Number) && id.length !== 12 && id.length !== 24) {
        return false;
      }

      if ((typeof id === 'string' || id instanceof String) && id.length === 24) {
        return /^[0-9a-fA-F]{24}$/i.test(id);
      }

      return true;
    }

    function getDate(id) {
      if (!isValid(id)) {
        return undefined;
      }

      return create(id).getDate();
    }

    var service = {
      create: create,
      isValid: isValid,
      getDate: getDate
    };

    return service;
  });
}());
