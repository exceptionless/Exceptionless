import { beforeEach, describe, expect, it } from 'vitest';

import { dismissOrganizationNotification, getDismissalPeriod, isOrganizationNotificationDismissed, pruneExpiredDismissals } from './notification-dismissals.ts';

describe('organization notification dismissals', () => {
    beforeEach(() => {
        localStorage.clear();
        // Dismissing for a long-past day prunes every other entry, leaving nothing that matches the dates below.
        dismissOrganizationNotification('trial-ending', 'reset', new Date(2000, 0, 1));
    });

    it('hides the trial-ending banner until the next local day', () => {
        const evening = new Date(2026, 9, 22, 21, 0);

        dismissOrganizationNotification('trial-ending', 'organization-id', evening);

        expect(isOrganizationNotificationDismissed('trial-ending', 'organization-id', new Date(2026, 9, 22, 23, 59))).toBe(true);
        expect(isOrganizationNotificationDismissed('trial-ending', 'organization-id', new Date(2026, 9, 23, 0, 1))).toBe(false);
        expect(isOrganizationNotificationDismissed('trial-ending', 'other-organization-id', evening)).toBe(false);
    });

    it('hides the usage warning for the rest of the UTC month', () => {
        dismissOrganizationNotification('usage-warning', 'organization-id', new Date('2026-10-02T12:00:00Z'));

        expect(isOrganizationNotificationDismissed('usage-warning', 'organization-id', new Date('2026-10-31T23:59:00Z'))).toBe(true);
        expect(isOrganizationNotificationDismissed('usage-warning', 'organization-id', new Date('2026-11-01T00:01:00Z'))).toBe(false);
        expect(isOrganizationNotificationDismissed('trial-ending', 'organization-id', new Date('2026-10-02T12:00:00Z'))).toBe(false);
    });

    it('drops dismissals from earlier periods', () => {
        const now = new Date('2026-11-01T12:00:00Z');
        const current = {
            'trial-ending:old': '2026-10-01',
            'usage-warning:current': getDismissalPeriod('usage-warning', now),
            'usage-warning:old': '2026-10'
        };

        expect(pruneExpiredDismissals(current, now)).toEqual({ 'usage-warning:current': getDismissalPeriod('usage-warning', now) });
    });

    it('ignores a missing organization', () => {
        expect(isOrganizationNotificationDismissed('usage-warning', undefined)).toBe(false);
    });
});
