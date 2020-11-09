(function () {
  'use strict';

  describe('Directive: duration', function () {

    beforeEach(module('exceptionless.duration'));
    moment.locale('en');
    
    var scope, compile;

    beforeEach(inject(function ($rootScope, $compile) {
      scope = $rootScope.$new();
      compile = $compile;
    }));

    it('should set the elements text to 10 seconds', function () {
      var element = compile('<duration value="10" />')(scope);
      expect(element.text()).toBe('a few seconds');
    });
  });
}());
