import type { CountResult } from '$shared/models';

import { describe, expect, it } from 'vitest';

import { tagSuggestionAggregation, tagSuggestions, tagSuggestionSession } from './tag-suggestions';

function result(size: number, data: Record<string, unknown> = { '@type': 'bucket' }): CountResult {
    const aggregate = { data, items: Array.from({ length: size }, (_, index) => ({ key: `tag-${index}`, total: 1 })) };
    return { aggregations: { terms_tags: aggregate }, total: size };
}

describe('tag aggregation bounds and completeness', () => {
    it('requests an overflow bucket initially and bounds matching searches', () => {
        expect(tagSuggestionAggregation('')).toBe('terms:(tags~251)');
        expect(tagSuggestionAggregation('Ab')).toBe('terms:(tags~250 @include:/.*[aA][bB].*/)');
    });

    it('only treats known repository metadata with no omitted terms as complete', () => {
        expect(tagSuggestions(result(250)).complete).toBe(true);
        expect(tagSuggestions(result(0)).complete).toBe(true);
        expect(tagSuggestions(result(1, { '@type': 'bucket', DocCountErrorUpperBound: 0, SumOtherDocCount: 0 })).complete).toBe(true);
        expect(tagSuggestions(result(251)).complete).toBe(false);
        expect(tagSuggestions(result(1, {})).complete).toBe(false);
        expect(tagSuggestions(undefined).complete).toBe(false);
        for (const counter of ['SumOtherDocCount', 'DocCountErrorUpperBound']) {
            for (const value of [1, -1, null, '0']) {
                expect(tagSuggestions(result(1, { '@type': 'bucket', [counter]: value })).complete).toBe(false);
            }
        }
    });

    it('never exposes the overflow bucket', () => {
        expect(tagSuggestions(result(251)).tags).toHaveLength(250);
    });

    it('escapes literal regex syntax and slash delimiters through both parsers', () => {
        expect(tagSuggestionAggregation('a.b+c/d\\e')).toBe(String.raw`terms:(tags~250 @include:/.*[aA]\\.[bB]\\+[cC]\/[dD]\\\\[eE].*/)`);
        expect(tagSuggestionAggregation('@#&<>~"(){}[]?*|')).toBe(String.raw`terms:(tags~250 @include:/.*\\@\\#\\&\\<\\>\\~\\"\\(\\)\\{\\}\\[\\]\\?\\*\\|.*/)`);
    });

    it('preserves non-ASCII letters without introducing multicharacter case expansions', () => {
        expect(tagSuggestionAggregation('éß')).toBe('terms:(tags~250 @include:/.*[éÉ]ß.*/)');
    });

    it('partitions cache sessions without placing credentials into keys', () => {
        const first = tagSuggestionSession('session-a');
        expect(tagSuggestionSession('session-a')).toBe(first);
        expect(tagSuggestionSession('session-b')).not.toBe(first);
        tagSuggestionSession(null);
        expect(tagSuggestionSession('session-a')).not.toBe(first);
    });
});
