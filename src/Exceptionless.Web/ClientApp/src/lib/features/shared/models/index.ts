export { CountResult, type IAggregate, WorkInProgressResult } from '$generated/api';

export * from './aggregations';

// TODO: Fix api code gen
export class ValueFromBody<T> {
    constructor(public value: T) {}
}
