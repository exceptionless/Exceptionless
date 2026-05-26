import type { ReleaseNotification, SystemNotification } from '$features/websockets/models';

export type { ReleaseNotification, SystemNotification };

export interface ForceRefreshRequest {
    message?: null | string;
}

export interface NotificationSettings {
    configured_system_notification_message?: null | string;
    system_notification?: null | SystemNotification;
}

export interface SendReleaseNotificationRequest {
    critical?: boolean;
    message?: null | string;
}

export interface SetSystemNotificationRequest {
    message: string;
    publish?: boolean;
}
