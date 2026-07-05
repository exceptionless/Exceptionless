(function() {
	var slides_selector = '.cycloneslider-template-standard .cycloneslider-slides';
	
	jQuery(document).on('cycle-before', slides_selector, function( event, optionHash, outgoingSlideEl, incomingSlideEl, forwardFlag ) {
		var slide = jQuery( outgoingSlideEl ); /* Current slide */
		
		if(optionHash.dynamicHeight == "on" && ((optionHash.autoHeight+"").indexOf(":") == -1) ) jQuery(this).animate({height:jQuery(incomingSlideEl).outerHeight()}, optionHash.autoHeightSpeed, optionHash.autoHeightEasing); /* Autoheight when dynamic height is on and auto height is not ratio (eg. 300:250) */
		
		if(slide.hasClass('cycloneslider-slide-youtube')) pauseYoutube( slide ); /* Pause youtube video on next */
		
		if(slide.hasClass('cycloneslider-slide-vimeo')) pauseVimeo( slide ); /* Pause vimeo video on next */
	});
	
    jQuery(document).on('cycle-initialized cycle-after', slides_selector, function( event, optionHash, outgoingSlideEl, incomingSlideEl, forwardFlag ) {
		var index = (event.type == 'cycle-initialized') ? optionHash.currSlide : optionHash.nextSlide;
		var slide = jQuery( optionHash.slides[ index ] );
		slide.css('zIndex', parseInt(slide.css('zIndex'))+20); /* Fix for slideshow with youtube slide */
	});
	
	function pauseYoutube( slide ){
		var data = {
			"event": "command",
			"func": "pauseVideo",
			"args": [],
			"id": ""
		}
		postMessage( slide.find('iframe'), data, '*');
	}
	
	function pauseVimeo( slide ){
		postMessage( slide.find('iframe'), {method:'pause'}, slide.find('iframe').attr('src'));
	}
	
	function postMessage(iframe, data, url){
		try{
			if (iframe[0]) { // Frame exists
				iframe[0].contentWindow.postMessage(JSON.stringify(data), url);
			}
		} catch (ignore) {}
	}
	
})();
