import { ExceptionlessClient } from '@exceptionless/browser';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { submitProductTourActivity } from './activity';

const submitFeatureUsage = vi.hoisted(() => vi.fn());
vi.mock('$features/auth/exceptionless-session', () => ({ submitFeatureUsage }));

describe('product-tour activity', () => {
    beforeEach(() => vi.resetAllMocks());

    it.each(['completed', 'dismissed'] as const)('uses the existing feature-usage pipeline for %s', async (action) => {
        // Act
        await submitProductTourActivity(action, 'app-overview');

        // Assert
        expect(submitFeatureUsage).toHaveBeenCalledExactlyOnceWith(`product-tour.${action}.app-overview`);
    });

    it('does not propagate telemetry failures into guide navigation', async () => {
        // Arrange
        submitFeatureUsage.mockRejectedValue(new Error('Unavailable'));

        // Act & Assert
        await expect(submitProductTourActivity('completed', 'app-overview')).resolves.toBeUndefined();
    });

    it.each(['', 'synthetic-local-test-key'])('honors the existing SDK configuration (key: %s)', async (apiKey) => {
        // Arrange
        const client = new ExceptionlessClient();
        client.config.apiKey = apiKey;
        const enqueue = vi.spyOn(client.config.services.queue, 'enqueue').mockResolvedValue(undefined);
        submitFeatureUsage.mockImplementation((feature: string) => client.submitFeatureUsage(feature));

        // Act
        await submitProductTourActivity('completed', 'app-overview');

        // Assert
        expect(enqueue).toHaveBeenCalledTimes(apiKey ? 1 : 0);
        if (apiKey) {
            expect(enqueue).toHaveBeenCalledWith(expect.objectContaining({ source: 'product-tour.completed.app-overview', type: 'usage' }));
        }
    });
});
