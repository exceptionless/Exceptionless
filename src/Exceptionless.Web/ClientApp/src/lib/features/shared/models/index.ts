export { CountResult, type IAggregate, WorkInProgressResult } from '$generated/api';

// TODO: Fix api code gen
export class ValueFromBody<T> {
    constructor(public value: T) {}
}

export * from './aggregations';
