import { describe, expect, it } from 'vitest';

import { getUtcMonthKey } from './utils';

describe('getUtcMonthKey', () => {
    it('changes only when the UTC month changes', () => {
        expect(getUtcMonthKey(new Date('2026-08-01T00:00:00.000Z'))).toBe(getUtcMonthKey(new Date('2026-08-31T23:59:59.999Z')));
        expect(getUtcMonthKey(new Date('2026-09-01T00:00:00.000Z'))).not.toBe(getUtcMonthKey(new Date('2026-08-31T23:59:59.999Z')));
    });
});
