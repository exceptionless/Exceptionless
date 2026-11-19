import { describe, expect, it } from 'vitest';

import { getChecklistItems, isChecklistComplete, shouldShowChecklist } from './checklist';

describe('getting started checklist', () => {
    it('derives data items from the account and visit items from recorded keys', () => {
        const items = getChecklistItems({
            emailVerified: false,
            hasEvents: true,
            hasProject: true,
            productTours: { checklist_ai_tools: '2026-10-01T00:00:00Z' }
        });

        expect(Object.fromEntries(items.map((item) => [item.id, item.complete]))).toEqual({
            account: true,
            'ai-tools': true,
            'first-event': true,
            'invite-team': false,
            notifications: false,
            project: true,
            'verify-email': false,
            versions: false
        });
        expect(isChecklistComplete(items)).toBe(false);
    });

    it('is complete when every item is done', () => {
        const items = getChecklistItems({
            emailVerified: true,
            hasEvents: true,
            hasProject: true,
            productTours: {
                checklist_ai_tools: '2026-10-01T00:00:00Z',
                checklist_invite_team: '2026-10-01T00:00:00Z',
                checklist_notifications: '2026-10-01T00:00:00Z',
                checklist_versions: '2026-10-01T00:00:00Z'
            }
        });

        expect(isChecklistComplete(items)).toBe(true);
    });

    it('lists asking Exie after verifying the email only when Exie is offered', () => {
        const input = { emailVerified: true, hasEvents: true, hasProject: true };

        expect(getChecklistItems(input).map((item) => item.id)).not.toContain('ask-exie');

        const items = getChecklistItems({ ...input, includeAskExie: true, productTours: { checklist_ask_exie: '2026-10-01T00:00:00Z' } });
        expect(items.map((item) => item.id).slice(3, 5)).toEqual(['verify-email', 'ask-exie']);
        expect(items.find((item) => item.id === 'ask-exie')).toEqual({ complete: true, id: 'ask-exie', kind: 'visit' });
    });

    it('shows only for new organizations that have not dismissed it', () => {
        const now = new Date('2026-10-21T00:00:00Z');

        expect(shouldShowChecklist({ now, organizationCreatedUtc: '2026-10-20T00:00:00Z' })).toBe(true);
        expect(shouldShowChecklist({ now, organizationCreatedUtc: '2026-08-01T00:00:00Z' })).toBe(false);
        expect(shouldShowChecklist({ now, organizationCreatedUtc: undefined })).toBe(false);
        expect(
            shouldShowChecklist({ now, organizationCreatedUtc: '2026-10-20T00:00:00Z', productTours: { checklist_dismissed: '2026-10-20T01:00:00Z' } })
        ).toBe(false);
    });
});
