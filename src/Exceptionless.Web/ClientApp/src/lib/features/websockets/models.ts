export enum ChangeType {
    Added = 0,
    Saved = 1,
    Removed = 2
}

export interface EntityChanged {
    change_type: ChangeType;
    data: Record<string, unknown>;

    id?: string;
    organization_id?: string;
    project_id?: string;
    stack_id?: string;

    type: string;
}

export interface PlanChanged {
    organization_id: string;
}

export interface PlanOverage {
    is_hourly: boolean;
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

export interface UserMembershipChanged {
    change_type: ChangeType;
    organization_id: string;
    user_id: string;
}

export interface WebSocketMessage<T extends WebSocketMessageType> {
    message: WebSocketMessageValue<T>;
    type: T;
}

export type WebSocketMessageType = 'PlanChanged' | 'PlanOverage' | 'ReleaseNotification' | 'SystemNotification' | 'UserMembershipChanged' | `${string}Changed`;

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

export function isEntityChangedType(message: { message: unknown; type: WebSocketMessageType }): message is WebSocketMessage<`${string}Changed`> {
    return message.type !== 'PlanChanged' && message.type !== 'UserMembershipChanged' && message.type.endsWith('Changed');
}

export function isWebSocketMessageType(type: string): type is WebSocketMessageType {
    return (
        (['PlanChanged', 'PlanOverage', 'UserMembershipChanged', 'ReleaseNotification', 'SystemNotification'] as const).includes(
            type as Exclude<WebSocketMessageType, `${string}Changed`>
        ) || type.endsWith('Changed')
    );
}
