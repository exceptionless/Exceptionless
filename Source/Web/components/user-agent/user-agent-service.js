/*global UAParser:false */

(function () {
  'use strict';

  angular.module('exceptionless.user-agent', [])
    .factory('userAgentService', [function () {
      function getUserAgent(userAgent) {
        var parser = new UAParser();
        parser.setUA(userAgent);
        return parser.getResult();
      }

      function getBrowser(userAgent) {
        var browser = getUserAgent(userAgent).browser;
        return browser.name + ' ' + browser.version;
      }

      function getBrowserOS(userAgent) {
        var os = getUserAgent(userAgent).os;
        return os.name + ' ' + os.version;
      }

      function getDevice(userAgent) {
        var device = getUserAgent(userAgent).device;
        return device.model;
      }

      var service = {
        getBrowser: getBrowser,
        getBrowserOS: getBrowserOS,
        getDevice: getDevice
      };

      return service;
    }
    ]);
}());
