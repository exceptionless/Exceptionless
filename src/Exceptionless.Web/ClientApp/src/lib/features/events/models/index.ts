import type { PersistentEvent as PersistentEventBase, UserDescription } from '$generated/api';

import type { EnvironmentInfo, ErrorInfo, LogLevel, ManualStackingInfo, RequestInfo, SimpleErrorInfo, UserInfo } from './event-data';

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

export interface PersistentEvent extends PersistentEventBase {
    data?: IPersistentEventData;
    type?: PersistentEventKnownTypes;
}

export type PersistentEventKnownTypes = '404' | 'error' | 'heartbeat' | 'log' | 'session' | 'sessionend' | 'usage' | string;

export interface SubmissionClient {
    ip_address?: string;
    user_agent?: string;
    version?: string;
}
