/*global li:false, queryString: false */
(function () {
  'use strict';

  angular.module('exceptionless.link', [])
    .factory('linkService', [function () {
      function getLinks(linkHeader) {
        if (linkHeader == null)
          return {};

        return li.parse(linkHeader || {});
      }

      function getLinksQueryParameters(linkHeader) {
        var parsedLinks = getLinks(linkHeader);
        var links = {};
        for (var rel in parsedLinks) {
          var url = parsedLinks[rel];
          links[rel] = queryString.parse(url.slice(url.indexOf('?')));
        }

        return links;
      }

      var service = {
        getLinks: getLinks,
        getLinksQueryParameters: getLinksQueryParameters
      };

      return service;
    }]);
}());
