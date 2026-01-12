export type { CountResult, IAggregate, StringValueFromBody, WorkInProgressResult } from '$generated/api';

export * from './aggregations';

export interface CustomDateRange {
    end?: string;
    start?: string;
}
