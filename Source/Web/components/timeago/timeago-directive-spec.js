(function () {
  'use strict';

  describe('Directive: timeago', function () {

    beforeEach(module('exceptionless.timeago'));

    var scope, compile;

    beforeEach(inject(function ($rootScope, $compile) {
      scope = $rootScope.$new();
      compile = $compile;
    }));

    it('should set the elements text to a few seconds ago', function () {
      scope.date = new Date();
      var element = compile('<timeago date="date" />')(scope);
      expect(element.text()).toBe('a few seconds ago');
    });

    it('should set the elements text to not a few seconds ago', function () {
      scope.date = new moment('2014-10-02T07:09:18.2971368').toDate();
      var element = compile('<timeago date="date" />')(scope);
      expect(element.text()).not.toBe('a few seconds ago');
    });
  });
}());
