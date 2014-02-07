(function($){
    
    /* ----
       superLink jQuery plugin
       (c) James Padolsey
       ----
       Contributors:
            Brian Fisher
    */
    
    $.fn.superLink = function(link) {
        link = link || 'a:first';
        return this.each(function(){
            
            var $container = $(this),
                $targetLink = $(link, $container).clone(true);
            
            /* Take all current mouseout handlers of $container and
               transfer them to the mouseout handler of $targetLink */
            var mouseouts = $(this).data('events') && $(this).data('events').mouseout;
            if (mouseouts) {
                $.each(mouseouts, function(i, fn){
                    $targetLink.mouseout(function(e){
                        fn.call($container, e);
                    });
                });
                delete $(this).data('events').mouseout;
            }
            
            /* Take all current mouseover handlers of $container and
               transfer them to the mouseover handler of $targetLink */
            var mouseovers = $(this).data('events') && $(this).data('events').mouseover;
            if (mouseovers) {
                $.each(mouseovers, function(i, fn){
                    $targetLink.mouseover(function(e){
                        fn.call($container, e);
                    });
                });
                delete $(this).data('events').mouseover;
            }
            
            $container.mouseover(function(){
                $targetLink.show();
            });
            
            $targetLink
                .click(function(){
                    $targetLink.blur();
                })
                .mouseout(function(e){
                    $targetLink.hide();
                })
                .css({
                    position: 'absolute',
                    top: $container.offset().top,
                    left: $container.offset().left,
                    /* IE requires background to be set */
                    backgroundColor: '#FFF',
                    display: 'none',
                    opacity: 0,
                    width: $container.outerWidth(),
                    height: $container.outerHeight(),
                    padding: 0
                })
                .appendTo('body');
                
        });
    };
    
})(jQuery);