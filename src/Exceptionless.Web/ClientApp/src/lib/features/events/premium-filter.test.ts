import { describe, expect, it } from 'vitest';

import { filterUsesPremiumFeatures, getSearchResourceForPathname } from './premium-filter';

describe('filterUsesPremiumFeatures', () => {
    it.each([undefined, null, '', 'status:open', '(status:open OR status:regressed)', 'reference:ABC123'])('allows free event filters: %s', (filter) => {
        expect(filterUsesPremiumFeatures(filter, 'event')).toBe(false);
    });

    it.each(['tags:important', 'data.@user.identity:blake', 'data.Windows-identity:ejsmith', 'message:"out of memory"', '-tags:important', '+tags:important'])(
        'detects premium event filters: %s',
        (filter) => {
            expect(filterUsesPremiumFeatures(filter, 'event')).toBe(true);
        }
    );

    it.each([
        'first_occurrence:[now-1d TO now]',
        'last:now',
        'occurrences_are_critical:true',
        'critical:false',
        'project:ABC123',
        'reference:ABC123',
        'reference_id:ABC123',
        'stack:ABC123',
        'stack_id:ABC123',
        'reference:ABC123 first:true'
    ])('allows free stack-mode event filters: %s', (filter) => {
        expect(filterUsesPremiumFeatures(filter, 'event-stack')).toBe(false);
    });

    it.each(['title:"out of memory"', 'tags:important'])('detects premium stack-mode event filters: %s', (filter) => {
        expect(filterUsesPremiumFeatures(filter, 'event-stack')).toBe(true);
    });

    it.each(['critical:false', 'first_occurrence:[now-1d TO now]', 'project:ABC123', 'status:open'])('allows free direct stack filters: %s', (filter) => {
        expect(filterUsesPremiumFeatures(filter, 'stack')).toBe(false);
    });

    it.each(['reference:ABC123', 'stack:ABC123', 'stack_id:ABC123', 'title:"out of memory"', 'tags:important'])(
        'detects premium direct stack filters: %s',
        (filter) => {
            expect(filterUsesPremiumFeatures(filter, 'stack')).toBe(true);
        }
    );

    it('uses different rules for stack-mode event and direct stack searches', () => {
        expect(filterUsesPremiumFeatures('stack:ABC123', 'event-stack')).toBe(false);
        expect(filterUsesPremiumFeatures('stack:ABC123', 'stack')).toBe(true);
    });

    it('detects a premium field after a free field', () => {
        expect(filterUsesPremiumFeatures('status:open AND tags:important', 'event')).toBe(true);
    });
});

describe('getSearchResourceForPathname', () => {
    it.each(['/stack', '/next/stack/saved-view'])('identifies stack-mode event search routes: %s', (pathname) => {
        expect(getSearchResourceForPathname(pathname)).toBe('event-stack');
    });

    it.each(['/project/537650f3b77efe23a47914f4/stacks', '/next/project/537650f3b77efe23a47914f4/stacks/537650f3b77efe23a47914f5'])(
        'identifies direct stack search routes: %s',
        (pathname) => {
            expect(getSearchResourceForPathname(pathname)).toBe('stack');
        }
    );

    it.each(['/event', '/stream', '/sessions'])('identifies event search routes: %s', (pathname) => {
        expect(getSearchResourceForPathname(pathname)).toBe('event');
    });
});
