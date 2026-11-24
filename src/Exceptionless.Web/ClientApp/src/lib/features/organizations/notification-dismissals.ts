import { PersistedState } from 'runed';

/**
 * Organization notifications a user can hide for a while. Each dismissal lasts for a period:
 * the trial-ending banner until the next local day, the usage warning until the next UTC (billing) month.
 */
export type DismissibleOrganizationNotification = 'trial-ending' | 'usage-warning';

type Dismissals = Record<string, string>;

const dismissals = new PersistedState<Dismissals>('organization-notification-dismissals', {});

export function dismissOrganizationNotification(notification: DismissibleOrganizationNotification, organizationId: string, now: Date = new Date()): void {
    dismissals.current = {
        ...pruneExpiredDismissals(dismissals.current, now),
        [getDismissalKey(notification, organizationId)]: getDismissalPeriod(notification, now)
    };
}

export function getDismissalPeriod(notification: DismissibleOrganizationNotification, now: Date = new Date()): string {
    if (notification === 'usage-warning') {
        return `${now.getUTCFullYear()}-${String(now.getUTCMonth() + 1).padStart(2, '0')}`;
    }

    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
}

export function isOrganizationNotificationDismissed(
    notification: DismissibleOrganizationNotification,
    organizationId: string | undefined,
    now: Date = new Date()
): boolean {
    if (!organizationId) {
        return false;
    }

    return dismissals.current?.[getDismissalKey(notification, organizationId)] === getDismissalPeriod(notification, now);
}

export function pruneExpiredDismissals(current: Dismissals | undefined, now: Date = new Date()): Dismissals {
    return Object.fromEntries(
        Object.entries(current ?? {}).filter(([key, period]) => {
            const notification = key.slice(0, key.indexOf(':')) as DismissibleOrganizationNotification;
            return period === getDismissalPeriod(notification, now);
        })
    );
}

function getDismissalKey(notification: DismissibleOrganizationNotification, organizationId: string): string {
    return `${notification}:${organizationId}`;
}
