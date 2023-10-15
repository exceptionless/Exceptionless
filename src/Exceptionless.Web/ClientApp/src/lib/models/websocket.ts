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
