(function () {
  'use strict';

  angular.module('exceptionless.ui-shift', [])
    .directive('uiShift', function ($timeout) {
      return {
        restrict: 'A',
        link: function (scope, el, attr) {
          // get the $prev or $parent of this el
          var _el = $(el),
            _window = $(window),
            prev = _el.prev(),
            parent,
            width = _window.width()
            ;

          if (!prev.length) {
            parent = _el.parent();
          }

          function sm() {
            $timeout(function () {
              var method = attr.uiShift;
              var target = attr.target;
              if (!_el.hasClass('in')) {
                _el[method](target).addClass('in');
              }
            });
          }

          function md() {
            if (parent) {
              parent['prepend'](el);
            }

            if (!parent) {
              _el['insertAfter'](prev);
            }

            _el.removeClass('in');
          }

          if (width < 768) {
            sm();
          } else {
            md();
          }

          _window.resize(function () {
            if (width !== _window.width()) {
              $timeout(function () {
                if (_window.width() < 768) {
                  sm();
                } else {
                  md();
                }

                width = _window.width();
              });
            }
          });
        }
      };
    });
}());
