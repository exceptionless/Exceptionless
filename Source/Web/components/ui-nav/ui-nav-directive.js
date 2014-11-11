(function () {
  'use strict';

  angular.module('exceptionless.ui-nav', [])
    .directive('uiNav', ['$timeout', function ($timeout) {
      return {
        restrict: 'AC',
        link: function (scope, el, attr) {
          var _window = $(window),
            _mb = 768,
            wrap = $('.app-aside'),
            next,
            backdrop = '.dropdown-backdrop';
          // unfolded
          el.on('click', 'a', function (e) {
            if (next)
              next.trigger('mouseleave.nav');
            var _this = $(this);
            _this.parent().siblings(".active").toggleClass('active');
            if (_this.next().is('ul')) {
              _this.parent().toggleClass('active');
              e.preventDefault();
            }

            // mobile
            if (_this.next().is('ul') || (_window.width() < _mb )) {
              $('.app-aside').removeClass('show off-screen');
            }
          });

          // folded & fixed
          el.on('mouseenter', 'a', function (e) {
            if (next)
              next.trigger('mouseleave.nav');

            if (!$('.app-aside-fixed.app-aside-folded').length || ( _window.width() < _mb )) return;

            var _this = $(e.target), top, w_h = $(window).height(), offset = 50, min = 150;
            if (!_this.is('a'))
              _this = _this.closest('a');

            if (_this.next().is('ul')) {
              next = _this.next();
            } else {
              return;
            }

            _this.parent().addClass('active');
            top = _this.parent().position().top + offset;
            next.css('top', top);
            if (top + next.height() > w_h) {
              next.css('bottom', 0);
            }
            if (top + min > w_h) {
              next.css('bottom', w_h - top - offset).css('top', 'auto');
            }
            next.appendTo(wrap);

            next.on('mouseleave.nav', function (e) {
              $(backdrop).remove();
              next.appendTo(_this.parent());
              next.off('mouseleave.nav').css('top', 'auto').css('bottom', 'auto');
              _this.parent().removeClass('active');
            });

            if ($('.smart').length) {
              $('<div class="dropdown-backdrop"/>').insertAfter('.app-aside').on('click', function (next) {
                if (next)
                  next.trigger('mouseleave.nav');
              });
            }
          });

          wrap.on('mouseleave', function (e) {
            if (next)
              next.trigger('mouseleave.nav');
          });
        }
      };
    }]);
}());
