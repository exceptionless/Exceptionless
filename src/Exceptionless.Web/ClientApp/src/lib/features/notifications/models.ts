import type { ReleaseNotification, SystemNotification } from '$features/websockets/models';

export type { ReleaseNotification, SystemNotification };

export interface NotificationSettings {
    configured_system_notification_message?: null | string;
    system_notification?: null | SystemNotification;
}
