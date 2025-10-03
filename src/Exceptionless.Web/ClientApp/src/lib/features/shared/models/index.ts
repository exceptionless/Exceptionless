import { IsString } from 'class-validator';

export { CountResult, type IAggregate, WorkInProgressResult } from '$generated/api';

export * from './aggregations';

export class CustomDateRange {
    @IsString()
    end?: string;

    @IsString()
    start?: string;
}

// TODO: Fix api code gen
export class ValueFromBody<T> {
    constructor(public value: T) {}
}
