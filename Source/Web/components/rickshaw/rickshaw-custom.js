/*global Rickshaw:false */

(function () {
  'use strict';

  Rickshaw.namespace('Rickshaw.Graph.RangeSelector');

  Rickshaw.Graph.RangeSelector = Rickshaw.Class.create({
    initialize: function (args) {
      var element = this.element = args.element;
      var graph = this.graph = args.graph;
      graph._selectionCallback = args.selectionCallback;
      var position = this.position = {};
      var selectionBox = this.selectionBox = $('<div class="rickshaw_range_selector"></div>');
      var loader = $('<div class="rickshaw_range_selector_loader"></div>');
      selectionBox.insertBefore(graph.element.firstChild);
      loader.insertBefore(graph.element.firstChild);
      this._addListeners();
      graph.onUpdate(function () {
        this.update();
      }.bind(this));
    },
    _addListeners: function () {
      var graph = this.graph;
      var position = this.position;
      var selectionBox = this.selectionBox;
      var selectionControl = false;
      var selectionDraw = function (startPointX) {
        graph.element.addEventListener('mousemove', function (event) {
          if (selectionControl === true) {
            event.stopPropagation();
            var deltaX;
            position.x = event.layerX;
            deltaX = Math.max(position.x, startPointX) - Math.min(position.x, startPointX);
            position.minX = Math.min(position.x, startPointX);
            position.maxX = position.minX + deltaX;

            selectionBox.css({
              'transition': 'none',
              'opacity': '1',
              'width': deltaX,
              'height': '100%',
              'left': position.minX,
              'top': '0'
            });
          } else {
            return false;
          }
        }, false);
      };
      graph.element.addEventListener('mousedown', function (event) {
        event.stopPropagation();
        var startPointX = this.startPointX = event.layerX;
        selectionBox.css({
          'left': event.layerX,
          'height': '100%',
          'width': 0
        });
        selectionControl = true;
        selectionDraw(startPointX);
      }, true);
      document.body.addEventListener('keyup', function (event) {
        if (!selectionControl || selectionControl === false)
          return;

        event.stopPropagation();
        if (event.keyCode !== 27)
          return;

        selectionControl = false;
        selectionBox.css({
          'transition': 'opacity 0.2s ease-out',
          'opacity': '0',
          'width': 0,
          'height': 0
        });
      }, true);
      document.body.addEventListener('mouseup', function (event) {
        if (!selectionControl || selectionControl === false)
          return;

        selectionControl = false;
        position.coordMinX = Math.round(graph.x.invert(position.minX));
        position.coordMaxX = Math.round(graph.x.invert(position.maxX));
        selectionBox.css({
          'transition': 'opacity 0.2s ease-out',
          'width': 0,
          'height': 0,
          'opacity': '0'
        });

        if (graph._selectionCallback && !isNaN(position.coordMinX) && !isNaN(position.coordMaxX) &&
          this.startPointX !== event.layerX && // Ensure that there was an actual selection.
          event.button === 0) { // Only accept left mouse button up..
          graph._selectionCallback(position);
        }

        this.startPointX = 0;
      }, false);
    },
    update: function () {
      var graph = this.graph;
      var position = this.position;

      if (graph.window.xMin === null) {
        position.coordMinX = graph.dataDomain()[0];
      }

      if (graph.window.xMax === null) {
        position.coordMaxX = graph.dataDomain()[1];
      }
    }
  });
}());
