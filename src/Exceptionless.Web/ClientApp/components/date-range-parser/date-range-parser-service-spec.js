(function () {
  'use strict';

  describe('Service: Date Range Parser', function () {

    beforeEach(module('exceptionless.date-range-parser'));

    it('should parse two part date range', inject(function (dateRangeParserService) {
      var range = dateRangeParserService.parse('2014-11-12T00:00:00-2014-11-28T00:00:00');
      expect(range).toBeDefined();
      expect(range.start).toBe('2014-11-12T00:00:00');
      expect(range.end).toBe('2014-11-28T00:00:00');
    }));

    it('should not parse invalid range', inject(function (dateRangeParserService) {
      expect(dateRangeParserService.parse()).toBeNull();
    }));
  });
}());
