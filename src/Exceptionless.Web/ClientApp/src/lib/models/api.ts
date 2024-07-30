import { IsOptional, ValidateNested } from 'class-validator';

import type { EnvironmentInfo, ErrorInfo, LogLevel, ManualStackingInfo, RequestInfo, SimpleErrorInfo, UserInfo } from './client-data';

import { PersistentEvent as PersistentEventBase, StackStatus, UserDescription } from './api.generated';

export { Login, Stack, StackStatus, TokenResult, User, ViewOrganization, ViewProject } from './api.generated';

export type PersistentEventKnownTypes = '404' | 'error' | 'heartbeat' | 'log' | 'session' | 'sessionend' | 'usage' | string;

export type KnownDataKeys =
    | '@environment'
    | '@error'
    | '@level'
    | '@location'
    | '@request'
    | '@simple_error'
    | '@stack'
    | '@submission_client'
    | '@submission_method'
    | '@trace'
    | '@user'
    | '@user_description'
    | '@version'
    | 'sessionend';

export interface Location {
    country?: string;
    level1?: string;
    level2?: string;
    locality?: string;
}

export interface SubmissionClient {
    ip_address?: string;
    user_agent?: string;
    version?: string;
}

export interface IPersistentEventData extends Record<string, unknown> {
    '@environment'?: EnvironmentInfo;
    '@error'?: ErrorInfo;
    '@level'?: LogLevel;
    '@location'?: Location;
    '@request'?: RequestInfo;
    '@simple_error'?: SimpleErrorInfo;
    '@stack'?: ManualStackingInfo;
    '@submission_client'?: SubmissionClient;
    '@submission_method'?: string;
    '@trace'?: string[];
    '@user'?: UserInfo;
    '@user_description'?: UserDescription;
    '@version'?: string;
    haserror?: boolean;
    sessionend?: string;
}

export class PersistentEvent extends PersistentEventBase {
    @IsOptional() @ValidateNested() data?: IPersistentEventData = undefined;
    @IsOptional() type?: PersistentEventKnownTypes = undefined;
}

export type SummaryTemplateKeys =
    | 'event-error-summary'
    | 'event-feature-summary'
    | 'event-log-summary'
    | 'event-notfound-summary'
    | 'event-session-summary'
    | 'event-simple-summary'
    | 'event-summary'
    | 'stack-error-summary'
    | 'stack-feature-summary'
    | 'stack-log-summary'
    | 'stack-notfound-summary'
    | 'stack-session-summary'
    | 'stack-simple-summary'
    | 'stack-summary';

export type SummaryDataValue<T extends SummaryTemplateKeys> = T extends 'event-summary'
    ? EventSummaryData
    : T extends 'stack-summary'
      ? StackSummaryData
      : T extends 'event-simple-summary'
        ? EventSimpleSummaryData
        : T extends 'stack-simple-summary'
          ? StackSimpleSummaryData
          : T extends 'event-error-summary'
            ? EventErrorSummaryData
            : T extends 'stack-error-summary'
              ? StackErrorSummaryData
              : T extends 'event-session-summary'
                ? EventSessionSummaryData
                : T extends 'event-notfound-summary'
                  ? EventNotFoundSummaryData
                  : T extends 'event-feature-summary'
                    ? EventFeatureSummaryData
                    : T extends 'event-log-summary'
                      ? EventLogSummaryData
                      : T extends 'stack-log-summary'
                        ? StackLogSummaryData
                        : Record<string, unknown>;

export interface EventSummaryData {
    Identity?: string;
    Message?: string;
    Name?: string;
    Source?: string;
    Type?: string;
}

export interface StackSummaryData {
    Source?: string;
    Type?: string;
}

export interface EventSimpleSummaryData {
    Message?: string;
    Path?: string;
    Type?: string;
    TypeFullName?: string;
}

export interface StackSimpleSummaryData {
    Path?: string;
    Type?: string;
    TypeFullName?: string;
}

export interface EventErrorSummaryData {
    Message?: string;
    Method?: string;
    MethodFullName?: string;
    Path?: string;
    Type?: string;
    TypeFullName?: string;
}

export interface StackErrorSummaryData {
    Message?: string;
    Method?: string;
    MethodFullName?: string;
    Path?: string;
    Type?: string;
    TypeFullName?: string;
}

export interface EventSessionSummaryData {
    Identity?: string;
    Name?: string;
    SessionEnd?: string;
    SessionId?: string;
    Type?: 'heartbeat' | 'session' | 'sessionend';
    Value?: string;
}

export interface EventNotFoundSummaryData {
    Identity?: string;
    Name?: string;
    Source?: string;
}

export interface EventFeatureSummaryData {
    Identity?: string;
    IpAddress?: string[];
    Name?: string;
    Source?: string;
}

export interface EventLogSummaryData {
    Identity?: string;
    Level?: LogLevel;
    Message?: string;
    Name?: string;
    Source?: string;
    SourceShortName?: string;
}

export interface StackLogSummaryData {
    Source?: string;
    SourceShortName?: string;
}

export interface SummaryModel<T extends SummaryTemplateKeys> {
    data: SummaryDataValue<T>;
    id: string;
    template_key: T;
}

export interface EventSummaryModel<T extends SummaryTemplateKeys> extends SummaryModel<T> {
    /** @format date-time */
    date: string;
}

export interface StackSummaryModel<T extends SummaryTemplateKeys> extends SummaryModel<T> {
    /** @format date-time */
    first_occurrence: string;
    /** @format date-time */
    last_occurrence: string;
    status: StackStatus;
    title: string;
    /** @format int64 */
    total: number;
    /** @format double */
    total_users: number;
    /** @format double */
    users: number;
}

export type GetEventsMode = 'stack_frequent' | 'stack_new' | 'stack_recent' | 'stack_users' | 'summary' | null;

export interface IGetEventsParams {
    after?: string;
    before?: string;
    filter?: string;
    limit?: number;
    mode?: GetEventsMode;
    offset?: string;
    page?: number;
    sort?: string;
    time?: string;
}
