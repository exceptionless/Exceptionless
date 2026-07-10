import { describe, expect, it } from 'vitest';

import { filterUsesPremiumFeatures } from './premium-filter';

describe('filterUsesPremiumFeatures', () => {
    it.each([undefined, null, '', 'status:open', '(status:open OR status:regressed)', 'reference:ABC123'])('allows free event filters: %s', (filter) => {
        expect(filterUsesPremiumFeatures(filter, 'event')).toBe(false);
    });

    it.each(['tags:important', 'data.user.identity:blake', 'message:"out of memory"'])('detects premium event filters: %s', (filter) => {
        expect(filterUsesPremiumFeatures(filter, 'event')).toBe(true);
    });

    it.each(['first_occurrence:[now-1d TO now]', 'last:now', 'occurrences_are_critical:true', 'critical:false', 'project:ABC123'])(
        'allows free stack filters: %s',
        (filter) => {
            expect(filterUsesPremiumFeatures(filter, 'stack')).toBe(false);
        }
    );

    it.each(['title:"out of memory"', 'reference:ABC123', 'stack:ABC123', 'tags:important'])('detects premium stack filters: %s', (filter) => {
        expect(filterUsesPremiumFeatures(filter, 'stack')).toBe(true);
    });

    it('detects a premium field after a free field', () => {
        expect(filterUsesPremiumFeatures('status:open AND tags:important', 'event')).toBe(true);
    });
});
