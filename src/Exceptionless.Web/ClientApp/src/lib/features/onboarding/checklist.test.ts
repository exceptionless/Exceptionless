import { describe, expect, it } from 'vitest';

import { getChecklistItems, isChecklistComplete, shouldShowChecklist } from './checklist';

const recordedAt = '2026-10-01T00:00:00Z';

describe('getting started checklist', () => {
    it('derives data items from the account and visit items from recorded keys', () => {
        // Arrange
        const input = { emailVerified: false, hasEvents: true, hasProject: true, productTours: { checklist_ai_tools: recordedAt } };

        // Act
        const items = getChecklistItems(input);

        // Assert
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
        // Arrange
        const items = getChecklistItems({
            emailVerified: true,
            hasEvents: true,
            hasProject: true,
            productTours: {
                checklist_ai_tools: recordedAt,
                checklist_invite_team: recordedAt,
                checklist_notifications: recordedAt,
                checklist_versions: recordedAt
            }
        });

        // Act
        const complete = isChecklistComplete(items);

        // Assert
        expect(complete).toBe(true);
    });

    it('leaves asking Exie off when Exie is not offered', () => {
        // Act
        const items = getChecklistItems({ emailVerified: true, hasEvents: true, hasProject: true });

        // Assert
        expect(items.map((item) => item.id)).not.toContain('ask-exie');
    });

    it('lists asking Exie right after verifying the email when Exie is offered', () => {
        // Arrange
        const input = { emailVerified: true, hasEvents: true, hasProject: true, includeAskExie: true, productTours: { checklist_ask_exie: recordedAt } };

        // Act
        const items = getChecklistItems(input);

        // Assert
        expect(items.map((item) => item.id).slice(3, 5)).toEqual(['verify-email', 'ask-exie']);
        expect(items.find((item) => item.id === 'ask-exie')).toEqual({ complete: true, id: 'ask-exie', kind: 'visit' });
    });

    it.each([
        ['a new organization', { organizationCreatedUtc: '2026-10-20T00:00:00Z' }, true],
        ['an organization older than 30 days', { organizationCreatedUtc: '2026-08-01T00:00:00Z' }, false],
        ['an unknown creation date', { organizationCreatedUtc: undefined }, false],
        ['a dismissed checklist', { organizationCreatedUtc: '2026-10-20T00:00:00Z', productTours: { checklist_dismissed: '2026-10-20T01:00:00Z' } }, false]
    ])('decides visibility for %s', (_scenario, input, expected) => {
        // Arrange
        const now = new Date('2026-10-21T00:00:00Z');

        // Act
        const visible = shouldShowChecklist({ now, ...input });

        // Assert
        expect(visible).toBe(expected);
    });
});
