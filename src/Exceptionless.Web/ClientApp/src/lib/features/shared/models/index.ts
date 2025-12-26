export type { CountResult, IAggregate, StringValueFromBody, WorkInProgressResult } from '$generated/api';

export * from './aggregations';

export class CustomDateRange {
    @IsString()
    end?: string;

    @IsString()
    start?: string;
}
