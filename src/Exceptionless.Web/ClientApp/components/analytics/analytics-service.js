/*global fbq:false */
(function () {
  'use strict';

  angular.module('exceptionless.analytics', [
    'angular-locker',

    'angulartics',
    'angulartics.google.tagmanager'
  ])
  .config(function ($analyticsProvider) {
    $analyticsProvider.registerPageTrack(function (path) {
      if (window.fbq) {
        fbq('track', 'PageView');
      }
    });

    $analyticsProvider.registerEventTrack(function (action, properties) {
      if (!window.fbq) {
        return;
      }

      properties = properties || {};
      var eventList = ['ViewContent', 'Search', 'AddToCart', 'AddToWishlist', 'InitiateCheckout', 'AddPaymentInfo', 'Purchase', 'Lead', 'CompleteRegistration'];
      if(eventList.indexOf(action) === -1) {
        fbq('trackCustom', action, properties);
      } else {
        fbq('track', action, properties);
      }
    });
  })
  .factory('analyticsService', function ($analytics, locker) {
    var _store = locker.driver('session').namespace('analytics');

    function addPaymentInfo() {
      return $analytics.eventTrack('AddPaymentInfo');
    }

    function completeRegistration(queryString) {
      var data = {};
      if (queryString && (queryString.domain || queryString.medium || queryString.type || queryString.campaign || queryString.content || queryString.keyword)) {
        data = {
          marketing_domain: queryString.domain,
          marketing_medium: queryString.medium,
          marketing_type: queryString.type,
          marketing_campaign: queryString.campaign,
          marketing_content: queryString.content,
          marketing_keyword: queryString.keyword
        };

        _store.put('registration', data);
      }

      return $analytics.eventTrack('CompleteRegistration', data);
    }

    function getRegistrationQueryStringData() {
      return _store.get('registration') || {};
    }

    function initiateCheckout() {
      return $analytics.eventTrack('InitiateCheckout');
    }

    function lead(data) {
      return $analytics.eventTrack('Lead', data);
    }

    function purchase(data) {
      return $analytics.eventTrack('Purchase', data);
    }

    var service = {
      addPaymentInfo: addPaymentInfo,
      completeRegistration: completeRegistration,
      getRegistrationQueryStringData: getRegistrationQueryStringData,
      initiateCheckout: initiateCheckout,
      lead: lead,
      purchase: purchase
    };
    return service;
  });
}());
