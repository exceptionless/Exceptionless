import type { LogLevel } from '$features/events/models/event-data';
import type { StackStatus } from '$features/stacks/models';

export interface EventErrorSummaryData {
    Message?: string;
    Method?: string;
    MethodFullName?: string;
    Path?: string;
    Type?: string;
    TypeFullName?: string;
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

export interface EventNotFoundSummaryData {
    Identity?: string;
    Name?: string;
    Source?: string;
}

export interface EventSessionSummaryData {
    Identity?: string;
    Name?: string;
    SessionEnd?: string;
    SessionId?: string;
    Type?: 'heartbeat' | 'session' | 'sessionend';
    Value?: string;
}

export interface EventSimpleSummaryData {
    Message?: string;
    Path?: string;
    Type?: string;
    TypeFullName?: string;
}

export interface EventSummaryData {
    Identity?: string;
    Message?: string;
    Name?: string;
    Source?: string;
    Type?: string;
}

export interface EventSummaryModel<T extends SummaryTemplateKeys> extends SummaryModel<T> {
    /** @format date-time */
    date: string;
}

export interface StackErrorSummaryData {
    Message?: string;
    Method?: string;
    MethodFullName?: string;
    Path?: string;
    Type?: string;
    TypeFullName?: string;
}

export interface StackLogSummaryData {
    Source?: string;
    SourceShortName?: string;
}

export interface StackSimpleSummaryData {
    Path?: string;
    Type?: string;
    TypeFullName?: string;
}

export interface StackSummaryData {
    Source?: string;
    Type?: string;
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

export interface SummaryModel<T extends SummaryTemplateKeys> {
    data: SummaryDataValue<T>;
    id: string;
    template_key: T;
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
