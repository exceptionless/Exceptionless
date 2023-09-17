import type { StackStatus } from './api.generated';

export { Login, PersistentEvent, TokenResult } from './api.generated';

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
	Level?: string;
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

export enum ChangeType {
	Added = 0,
	Saved = 1,
	Removed = 2
}

export interface EntityChanged {
	type: string;
	change_type: ChangeType;

	id: string;
	organization_id?: string;
	projectId?: string;
	stackId?: string;

	data: Record<string, unknown>;
}

export interface PlanChanged {
	organization_id: string;
}

export interface PlanOverage {
	organization_id: string;
	is_hourly: boolean;
}

export interface UserMembershipChanged {
	change_type: ChangeType;
	user_id: string;
	organization_id: string;
}

export interface ReleaseNotification {
	critical: boolean;
	date: string;
	message?: string;
}

export interface SystemNotification {
	date: string;
	message?: string;
}

export type WebSocketMessageType =
	| 'PlanChanged'
	| 'PlanOverage'
	| 'UserMembershipChanged'
	| 'ReleaseNotification'
	| 'SystemNotification'
	| `${string}Changed`;

export type WebSocketMessageValue<T extends WebSocketMessageType> = T extends 'PlanChanged'
	? PlanChanged
	: T extends 'PlanOverage'
	? PlanOverage
	: T extends 'UserMembershipChanged'
	? UserMembershipChanged
	: T extends 'ReleaseNotification'
	? ReleaseNotification
	: T extends 'SystemNotification'
	? SystemNotification
	: EntityChanged;

export interface WebSocketMessage<T extends WebSocketMessageType> {
	type: T;
	message: WebSocketMessageValue<T>;
}

export function isWebSocketMessageType(type: string): type is WebSocketMessageType {
	return (
		(
			[
				'PlanChanged',
				'PlanOverage',
				'UserMembershipChanged',
				'ReleaseNotification',
				'SystemNotification'
			] as const
		).includes(type as Exclude<WebSocketMessageType, `${string}Changed`>) ||
		type.endsWith('Changed')
	);
}

export function isEntityChangedType(message: {
	type: WebSocketMessageType;
	message: unknown;
}): message is WebSocketMessage<`${string}Changed`> {
	return (
		message.type !== 'PlanChanged' &&
		message.type !== 'UserMembershipChanged' &&
		message.type.endsWith('Changed')
	);
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
