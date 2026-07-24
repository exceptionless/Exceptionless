import { describe, expect, it } from 'vitest';

import { filterUsesPremiumFeatures, getSearchResourceForPathname } from './premium-filter';

describe('filterUsesPremiumFeatures', () => {
    it.each([undefined, null, '', 'status:open', '(status:open OR status:regressed)', 'reference:ABC123'])('allows free event filters: %s', (filter) => {
        expect(filterUsesPremiumFeatures(filter, 'event')).toBe(false);
    });

    it.each(['tags:important', 'data.@user.identity:blake', 'message:"out of memory"', '-tags:important', '+tags:important'])(
        'detects premium event filters: %s',
        (filter) => {
            expect(filterUsesPremiumFeatures(filter, 'event')).toBe(true);
        }
    );

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

describe('getSearchResourceForPathname', () => {
    it.each(['/stack', '/next/stack/saved-view', '/project/537650f3b77efe23a47914f4/stacks'])('identifies stack search routes: %s', (pathname) => {
        expect(getSearchResourceForPathname(pathname)).toBe('stack');
    });

    it.each(['/event', '/stream', '/sessions'])('identifies event search routes: %s', (pathname) => {
        expect(getSearchResourceForPathname(pathname)).toBe('event');
    });
});
