export type { CountResult, IAggregate, StringValueFromBody, WorkInProgressResult } from '#generated/api.ts';

export * from './aggregations';

export interface CustomDateRange {
    end?: string;
    start?: string;
}
