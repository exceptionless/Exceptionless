(function () {
  'use strict';

  angular.module('exceptionless.ui-nav', [])
    .directive('uiNav', function () {
      return {
        restrict: 'AC',
        link: function(scope, element) {
          var _window = $(window);
          var _mb = 768;
          var _wrap = $('.app-aside');
          var _next;
          var _backdrop = '.dropdown-backdrop';

          element.on('click', 'a', function(e) {
            if (_next) {
              _next.trigger('mouseleave.nav');
            }

            var _this = $(this);
            _this.parent().siblings( ".active" ).toggleClass('active');

            if(_this.next().is('ul') && _this.parent().toggleClass('active') && e && e.preventDefault) {
              e.preventDefault();
            }

            // mobile
            if (_this.next().is('ul')) {
              return;
            }

            if (_window.width() < _mb) {
              $('.app-aside').removeClass('show off-screen');
            }
          });

          // folded & fixed
          element.on('mouseenter', 'a', function(e){
            if (_next) {
              _next.trigger('mouseleave.nav');
            }

            if (!$('.app-aside-fixed.app-aside-folded').length || (_window.width() < _mb)) {
              return;
            }

            var _this = $(e.target);
            var top;
            var w_h = $(window).height();
            var offset = 50;
            var min = 150;

            if(!_this.is('a')) {
              _this = _this.closest('a');
            }

            if( _this.next().is('ul') ){
              _next = _this.next();
            }else{
              return;
            }

            _this.parent().addClass('active');
            top = _this.parent().position().top + offset;
            _next.css('top', top);

            if( top + _next.height() > w_h ){
              _next.css('bottom', 0);
            }

            if(top + min > w_h){
              _next.css('bottom', w_h - top - offset).css('top', 'auto');
            }

            _next.appendTo(_wrap);

            _next.on('mouseleave.nav', function(e){
              $(_backdrop).remove();
              _next.appendTo(_this.parent());
              _next.off('mouseleave.nav').css('top', 'auto').css('bottom', 'auto');
              _this.parent().removeClass('active');
            });

            if($('.smart').length) {
              $('<div class="dropdown-backdrop"/>').insertAfter('.app-aside').on('click', function(next){
                if (next) {
                  next.trigger('mouseleave.nav');
                }
              });
            }
          });

          _wrap.on('mouseleave', function(e){
            if (_next) {
              _next.trigger('mouseleave.nav');
            }
          });
        }
      };
    });
}());
