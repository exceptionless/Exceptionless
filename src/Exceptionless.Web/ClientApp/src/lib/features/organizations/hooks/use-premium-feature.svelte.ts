import { getContext, onDestroy } from 'svelte';

const NOTIFICATION_CONTEXT_KEY = 'organizationNotifications';

/**
 * Triggers a premium feature notification for the current organization.
 * @param featureName Feature name to display in the notification.
 */
export function usePremiumFeature(featureName?: string) {
    const notifications = getContext(NOTIFICATION_CONTEXT_KEY) as undefined | { update: (fn: (n: any[]) => any[]) => void };
    let notificationId: null | number = null;

    if (notifications && featureName) {
        const id = Date.now() + Math.floor(Math.random() * 10000);
        notificationId = id;
        notifications.update((n: any[]) => [
            ...n,
            {
                feature: featureName,
                id,
                message: `The feature "${featureName}" is available on premium plans.`,
                timestamp: Date.now(),
                type: 'premium-feature'
            }
        ]);
    }

    onDestroy(() => {
        if (!notifications || notificationId == null) return;
        notifications.update((n: any[]) => n.filter((notif: any) => notif.id !== notificationId));
        notificationId = null;
    });
}
