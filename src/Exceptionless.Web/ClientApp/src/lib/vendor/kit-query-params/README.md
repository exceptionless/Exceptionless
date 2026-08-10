# kit-query-params

Vendored from [beynar/kit-query-params](https://github.com/beynar/kit-query-params) version 0.0.26 at commit `7c90edf7`.

The upstream project is MIT licensed; see [LICENSE](./LICENSE).

Local changes are intentionally covered by colocated tests:

- unchanged values do not write through the reactive proxy;
- URL synchronization is scheduled only for actual query-parameter changes;
- pending query-parameter synchronization is canceled when navigation begins;
- the source compiles under Exceptionless's stricter indexed-access TypeScript settings.
