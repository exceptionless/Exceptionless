(function () {
  'use strict';

  describe('Service: feature', function () {

    beforeEach(module('exceptionless.feature'));

    it('should set premium feature', inject(function (featureService) {
      expect(featureService.hasPremium(), false);
      featureService.setPremium(true);
      expect(featureService.hasPremium(), true);
    }));

    it('should not set invalid premium value', inject(function (featureService) {
      expect(featureService.hasPremium(), false);
      featureService.setPremium('test');
      expect(featureService.hasPremium(), false);
    }));
  });
}());
