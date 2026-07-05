(function ($) {
  $(function () {
    if ($ && $.fn && $.fn.fancybox) {
      $("a.fancybox").fancybox({ cyclic: true });
    }
  });
})(window.jQuery);