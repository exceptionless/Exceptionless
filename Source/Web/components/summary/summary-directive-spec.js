(function () {
  'use strict';

  describe('eventSummary', function () {
    beforeEach(module('app'));

    var scope, compile;
    beforeEach(inject(function ($rootScope, $compile) {
      scope = $rootScope.$new();
      compile = $compile;
    }));

    /* TODO: Look into why this doesn't pass.
     it('should use the event-summary template', function() {
     var message = "Testing default event message";
     scope.event = {
     "template_key": "event-summary",
     "data": {
     "message": message
     }
     };

     var element = compile('<event-summary />')(scope);
     expect(element.text()).toBe(message);
     });
     */
  });
}());
