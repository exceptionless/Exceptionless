import { describe, expect, it } from 'vitest';

import { buildProductTourTelemetryEvent } from './telemetry';

describe('product tour telemetry', () => {
    it('uses stable catalog metadata only', () => {
        expect(buildProductTourTelemetryEvent('step', 'new-ui-overview', 1, 'command-palette', 'command-search')).toBe(
            'product-tour.step.new-ui-overview.v1.command-palette.command-search'
        );
        expect(buildProductTourTelemetryEvent('announcement-started', 'exie-announcement', 1, 'feature-announcement')).toBe(
            'product-tour.announcement-started.exie-announcement.v1.feature-announcement'
        );
    });

    it('rejects resource data and invalid versions', () => {
        expect(() => buildProductTourTelemetryEvent('step', 'new-ui-overview', 1, 'catalog', 'Customer Project' as never)).toThrow();
        expect(() => buildProductTourTelemetryEvent('started', 'meet-exie', 0, 'catalog')).toThrow();
    });
});
