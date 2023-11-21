import { IsOptional, ValidateNested } from 'class-validator';

import {
	PersistentEvent as PersistentEventBase,
	StackStatus,
	UserDescription
} from './api.generated';

import type {
	EnvironmentInfo,
	ErrorInfo,
	LogLevel,
	ManualStackingInfo,
	RequestInfo,
	SimpleErrorInfo,
	UserInfo
} from './client-data';

export { Login, ViewProject, Stack, TokenResult } from './api.generated';

export type PersistentEventKnownTypes =
	| '404'
	| 'error'
	| 'heartbeat'
	| 'log'
	| 'usage'
	| 'session'
	| 'sessionend'
	| string;

export type KnownDataKeys =
	| '@environment'
	| '@error'
	| '@level'
	| '@location'
	| '@request'
	| '@simple_error'
	| '@stack'
	| '@submission_method'
	| '@submission_client'
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
	'@submission_method'?: string;
	'@submission_client'?: SubmissionClient;
	'@trace'?: string[];
	'@user'?: UserInfo;
	'@user_description'?: UserDescription;
	'@version'?: string;
	haserror?: boolean;
	sessionend?: string;
}

export class PersistentEvent extends PersistentEventBase {
	@IsOptional() type?: PersistentEventKnownTypes = undefined;
	@IsOptional() @ValidateNested() data?: IPersistentEventData = undefined;
}

export type SummaryTemplateKeys =
	| 'event-summary'
	| 'stack-summary'
	| 'event-simple-summary'
	| 'stack-simple-summary'
	| 'event-error-summary'
	| 'stack-error-summary'
	| 'event-session-summary'
	| 'stack-session-summary'
	| 'event-notfound-summary'
	| 'stack-notfound-summary'
	| 'event-feature-summary'
	| 'stack-feature-summary'
	| 'event-log-summary'
	| 'stack-log-summary';

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
	Message?: string;
	Source?: string;
	Type?: string;
	Identity?: string;
	Name?: string;
}

export interface StackSummaryData {
	Source?: string;
	Type?: string;
}

export interface EventSimpleSummaryData {
	Message?: string;
	Type?: string;
	TypeFullName?: string;
	Path?: string;
}

export interface StackSimpleSummaryData {
	Type?: string;
	TypeFullName?: string;
	Path?: string;
}

export interface EventErrorSummaryData {
	Message?: string;
	Type?: string;
	TypeFullName?: string;
	Method?: string;
	MethodFullName?: string;
	Path?: string;
}

export interface StackErrorSummaryData {
	Message?: string;
	Type?: string;
	TypeFullName?: string;
	Method?: string;
	MethodFullName?: string;
	Path?: string;
}

export interface EventSessionSummaryData {
	SessionId?: string;
	SessionEnd?: string;
	Value?: string;
	Type?: 'session' | 'sessionend' | 'heartbeat';
	Identity?: string;
	Name?: string;
}

export interface EventNotFoundSummaryData {
	Source?: string;
	Identity?: string;
	Name?: string;
}

export interface EventFeatureSummaryData {
	Source?: string;
	Identity?: string;
	Name?: string;
	IpAddress?: string[];
}

export interface EventLogSummaryData {
	Message?: string;
	Source?: string;
	SourceShortName?: string;
	Level?: LogLevel;
	Identity?: string;
	Name?: string;
}

export interface StackLogSummaryData {
	Source?: string;
	SourceShortName?: string;
}

export interface SummaryModel<T extends SummaryTemplateKeys> {
	id: string;
	template_key: T;
	data: SummaryDataValue<T>;
}

export interface EventSummaryModel<T extends SummaryTemplateKeys> extends SummaryModel<T> {
	/** @format date-time */
	date: string;
}

export interface StackSummaryModel<T extends SummaryTemplateKeys> extends SummaryModel<T> {
	title: string;
	status: StackStatus;
	/** @format date-time */
	first_occurrence: string;
	/** @format date-time */
	last_occurrence: string;
	/** @format int64 */
	total: number;
	/** @format double */
	users: number;
	/** @format double */
	total_users: number;
}

export type GetEventsMode =
	| 'summary'
	| 'stack_recent'
	| 'stack_frequent'
	| 'stack_new'
	| 'stack_users'
	| null;

export interface IGetEventsParams {
	filter?: string;
	sort?: string;
	time?: string;
	offset?: string;
	mode?: GetEventsMode;
	page?: number;
	limit?: number;
	before?: string;
	after?: string;
}
