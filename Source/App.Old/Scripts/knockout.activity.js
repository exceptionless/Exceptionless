// By: Hans Fjällemark and John Papa
// Rewritten by Alex Cornejo
// https://github.com/CodeSeven/KoLite

(function (factory) {
  if (typeof require === 'function' && typeof exports === 'object' && typeof module === 'object') {
    factory(require('knockout'), exports);
  } else if (typeof define === 'function' && define.amd) {
    define(['knockout', 'exports'], factory);
  } else {
    factory(ko);
  }
}(function (ko) {
  if (typeof ko === 'undefined') {
    throw 'Knockout is required, please ensure it is loaded before loading the activity plug-in';
  }

  var defaultOptions = {
    activityClass: 'fa fa-spinner fa-spin',
    container: 'i',
    inactiveClass: ''
  };

  function getOptions(overrides) {
    var options = ko.utils.extend({}, defaultOptions);

    return ko.utils.extend(options, overrides);
  }

  ko.bindingHandlers.activity = {
    init: function (element, valueAccessor, allBindingsAccessor) {
      var
        options = getOptions(allBindingsAccessor().activityOptions),
        activityIndicator = document.createElement(options.container);

      element.activityIndicator = activityIndicator;
      element.insertBefore(activityIndicator, element.firstChild);

      ko.utils.domNodeDisposal.addDisposeCallback(element, function () {
        element.removeChild(activityIndicator);
        delete element.activityIndicator;
      });
    },

    update: function (element, valueAccessor, allBindingsAccessor) {
      var
        options = getOptions(allBindingsAccessor().activityOptions),
        value = ko.utils.unwrapObservable(valueAccessor()),
        activity = typeof value === 'function' ? value() : value;

      if (activity) {
        element.activityIndicator.className = options.activityClass;
      } else {
        element.activityIndicator.className = options.inactiveClass;
      }
    }
  };
}));
