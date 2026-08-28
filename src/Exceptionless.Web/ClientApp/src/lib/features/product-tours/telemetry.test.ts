import { describe, expect, it } from 'vitest';

import { buildProductTourTelemetryEvent } from './telemetry';

describe('product tour telemetry', () => {
    it('records stable lifecycle events without resource data', () => {
        expect(buildProductTourTelemetryEvent('started', 'ui-overview', 1, 'command-palette')).toBe('product-tour.started.ui-overview.v1.command-palette');
    });

    it('rejects invalid versions', () => {
        expect(() => buildProductTourTelemetryEvent('started', 'meet-exie', 0, 'catalog')).toThrow();
    });
});
