describe('Service: link', function () {
  beforeEach(module('exceptionless.link'));

  it('should return previous and next links', inject(function (linkService) {
    var header = '<https://localhost/api/v2/events?limit=2&mode=summary&before=635460554443856362-5411e414a397230acc440091>; rel="previous", <https://localhost/api/v2/events?limit=2&mode=summary&after=635460554443856362-5411e415a397230acc440092>; rel="next"';
    var links = linkService.getLinks(header);
    expect(links.previous).toEqual('https://localhost/api/v2/events?limit=2&mode=summary&before=635460554443856362-5411e414a397230acc440091');
    expect(links.next).toEqual('https://localhost/api/v2/events?limit=2&mode=summary&after=635460554443856362-5411e415a397230acc440092');
  }));

  it('should return previous and next query parameters', inject(function (linkService) {
    var header = '<https://localhost/api/v2/events?limit=2&mode=summary&before=635460554443856362-5411e414a397230acc440091>; rel="previous", <https://localhost/api/v2/events?limit=2&mode=summary&after=635460554443856362-5411e415a397230acc440092>; rel="next"';
    var links = linkService.getLinksQueryParameters(header);
    expect(links.previous).not.toBeNull();
    expect(links.previous.limit).toEqual('2');
    expect(links.previous.mode).toEqual('summary');
    expect(links.previous.before).toEqual('635460554443856362-5411e414a397230acc440091');

    expect(links.next).not.toBeNull();
    expect(links.next.limit).toEqual('2');
    expect(links.next.mode).toEqual('summary');
    expect(links.next.after).toEqual('635460554443856362-5411e415a397230acc440092');
  }));
});
