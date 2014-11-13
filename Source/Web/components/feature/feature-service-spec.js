(function () {
  'use strict';

  describe('Service: feature', function () {

    beforeEach(module('exceptionless.feature'));

    it('should set premium feature', inject(function (featureService) {
      featureService.setPremium(false);

      expect(featureService.hasPremium()).toBe(false);
      featureService.setPremium(true);
      expect(featureService.hasPremium()).toBe(true);
    }));

    it('should not set invalid premium value', inject(function (featureService) {
      featureService.setPremium(false);

      expect(featureService.hasPremium()).toBe(false);
      featureService.setPremium('test');
      expect(featureService.hasPremium()).toBe(false);
    }));
  });
}());
