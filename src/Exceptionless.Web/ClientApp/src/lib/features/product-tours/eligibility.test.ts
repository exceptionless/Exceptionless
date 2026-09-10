import { describe, expect, it } from 'vitest';

import { getProductTourRecordedAt, isProductTourSetupRoute, shouldOfferProductTourInvitation } from './eligibility';

describe('product tour setup routes', () => {
    it.each(['/(app)/organization/add', '/(app)/project/add', '/(app)/project/[projectId]/configure'] as const)(
        'suppresses automatic tours on %s',
        (routeId) => {
            // Arrange: each case supplies a setup route ID.

            // Act
            const isSetup = isProductTourSetupRoute(routeId);

            // Assert
            expect(isSetup).toBe(true);
        }
    );

    it('allows automatic tours after setup', () => {
        // Arrange
        const routeId = '/(app)/stack';

        // Act
        const isSetup = isProductTourSetupRoute(routeId);
        const missingRouteIsSetup = isProductTourSetupRoute(null);

        // Assert
        expect(isSetup).toBe(false);
        expect(missingRouteIsSetup).toBe(false);
    });
});

describe('product tour invitation eligibility', () => {
    it('offers an invitation when no progress has been saved', () => {
        // Arrange
        const recordedUtc = undefined;

        // Act
        const eligible = shouldOfferProductTourInvitation(recordedUtc);

        // Assert
        expect(eligible).toBe(true);
    });

    it('does not offer an invitation after it has been recorded', () => {
        // Arrange
        const recordedUtc = '2026-09-08T00:00:00Z';

        // Act
        const eligible = shouldOfferProductTourInvitation(recordedUtc);

        // Assert
        expect(eligible).toBe(false);
    });
});

describe('persisted product tour state', () => {
    const recordedUtc = '2026-09-08T00:00:00Z';

    it('reads current timestamps and future UI-defined keys', () => {
        expect(getProductTourRecordedAt({ future_guide: recordedUtc }, 'future_guide')).toBe(recordedUtc);
        expect(getProductTourRecordedAt({}, 'app_overview')).toBeUndefined();
    });

    it('preserves legacy completion without treating a dismissed guide as complete', () => {
        const completed = { 'app-overview': { status: 'completed', updated_utc: recordedUtc, version: 1 } };
        const dismissed = { 'app-overview': { status: 'dismissed', updated_utc: recordedUtc, version: 1 } };

        expect(getProductTourRecordedAt(completed, 'app_overview')).toBe(recordedUtc);
        expect(getProductTourRecordedAt(dismissed, 'app_overview')).toBeUndefined();
    });

    it('keeps previously dismissed invitations hidden', () => {
        const state = { 'app-welcome': { status: 'dismissed', updated_utc: recordedUtc, version: 1 } };
        expect(shouldOfferProductTourInvitation(getProductTourRecordedAt(state, 'app_welcome', 'invitation'))).toBe(false);
    });

    it('prefers the current timestamp and ignores unrecognized values', () => {
        const state = { 'app-overview': { status: 'dismissed', updated_utc: '2020-01-01T00:00:00Z' }, app_overview: recordedUtc };
        expect(getProductTourRecordedAt(state, 'app_overview')).toBe(recordedUtc);
        expect(getProductTourRecordedAt({ app_overview: { future: true } }, 'app_overview')).toBeUndefined();
    });
});
