import type { CountResult } from '$shared/models';

import { describe, expect, it } from 'vitest';

import { tagSuggestionAggregation, tagSuggestions, tagSuggestionSession } from './tag-suggestions';

function result(size: number, data: Record<string, unknown> = { '@type': 'bucket' }): CountResult {
    const aggregate = { data, items: Array.from({ length: size }, (_, index) => ({ key: `tag-${index}`, total: 1 })) };
    return { aggregations: { terms_tags: aggregate }, total: size };
}

describe('tag aggregation bounds and completeness', () => {
    it.each([
        ['', 'terms:(tags~251)'],
        ['Ab', 'terms:(tags~250 @include:/.*[aA][bB].*/)'],
        ['a.b+c/d\\e', String.raw`terms:(tags~250 @include:/.*[aA]\\.[bB]\\+[cC]\/[dD]\\\\[eE].*/)`],
        ['@#&<>~"(){}[]?*|', String.raw`terms:(tags~250 @include:/.*\\@\\#\\&\\<\\>\\~\\"\\(\\)\\{\\}\\[\\]\\?\\*\\|.*/)`],
        ['éß', 'terms:(tags~250 @include:/.*[éÉ]ß.*/)']
    ])('bounds aggregation and preserves literal search %s', (search, expected) => {
        // Arrange
        const input = search;

        // Act
        const aggregation = tagSuggestionAggregation(input);

        // Assert
        expect(aggregation).toBe(expected);
    });

    it.each([
        { complete: true, data: { '@type': 'bucket' }, size: 250 },
        { complete: true, data: { '@type': 'bucket' }, size: 0 },
        { complete: true, data: { '@type': 'bucket', DocCountErrorUpperBound: 0, SumOtherDocCount: 0 }, size: 1 },
        { complete: false, data: { '@type': 'bucket' }, size: 251 },
        { complete: false, data: {}, size: 1 },
        { complete: false, data: { '@type': 'bucket', SumOtherDocCount: 1 }, size: 1 },
        { complete: false, data: { '@type': 'bucket', DocCountErrorUpperBound: 1 }, size: 1 },
        { complete: false, data: { '@type': 'bucket', SumOtherDocCount: -1 }, size: 1 },
        { complete: false, data: { '@type': 'bucket', DocCountErrorUpperBound: -1 }, size: 1 },
        { complete: false, data: { '@type': 'bucket', SumOtherDocCount: null }, size: 1 },
        { complete: false, data: { '@type': 'bucket', DocCountErrorUpperBound: null }, size: 1 },
        { complete: false, data: { '@type': 'bucket', SumOtherDocCount: '0' }, size: 1 },
        { complete: false, data: { '@type': 'bucket', DocCountErrorUpperBound: '0' }, size: 1 }
    ])('reports completeness only with authoritative metadata: %j', ({ complete, data, size }) => {
        // Arrange
        const response = result(size, data);

        // Act
        const suggestions = tagSuggestions(response);

        // Assert
        expect(suggestions.complete).toBe(complete);
    });

    it('does not treat a missing response as complete', () => {
        // Arrange
        const response = undefined;

        // Act
        const suggestions = tagSuggestions(response);

        // Assert
        expect(suggestions.complete).toBe(false);
    });

    it('never exposes the overflow bucket', () => {
        // Arrange
        const response = result(251);

        // Act
        const suggestions = tagSuggestions(response);

        // Assert
        expect(suggestions.tags).toHaveLength(250);
        expect(suggestions.tags).not.toContain('tag-250');
    });

    it('partitions changed credentials without placing them into keys', () => {
        // Arrange
        const first = tagSuggestionSession('session-a');

        // Act
        const repeated = tagSuggestionSession('session-a');
        const changed = tagSuggestionSession('session-b');

        // Assert
        expect(repeated).toBe(first);
        expect(changed).not.toBe(first);
    });
});
