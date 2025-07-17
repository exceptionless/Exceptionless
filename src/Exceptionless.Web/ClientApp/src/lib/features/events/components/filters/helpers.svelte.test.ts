import { describe, expect, it } from 'vitest';

import { quoteIfSpecialCharacters } from './helpers.svelte';

describe('helpers.svelte', () => {
    it('quoteIfSpecialCharacters handles tabs and newlines', () => {
        expect(quoteIfSpecialCharacters('foo\tbar')).toBe('"foo\tbar"');
        expect(quoteIfSpecialCharacters('foo\nbar')).toBe('"foo\nbar"');
    });

    it('quoteIfSpecialCharacters handles empty string and undefined/null', () => {
        expect(quoteIfSpecialCharacters('')).toBe('');
        expect(quoteIfSpecialCharacters(undefined)).toBeUndefined();
        expect(quoteIfSpecialCharacters(null)).toBeNull();
    });

    it('quoteIfSpecialCharacters quotes strings with multiple spaces', () => {
        expect(quoteIfSpecialCharacters('foo  bar')).toBe('"foo  bar"');
    });

    it('quoteIfSpecialCharacters quotes strings with leading and trailing spaces', () => {
        expect(quoteIfSpecialCharacters(' foo bar ')).toBe('"foo bar"');
    });

    it('quoteIfSpecialCharacters does not alter already quoted strings', () => {
        expect(quoteIfSpecialCharacters('"foo bar"')).toBe('"foo bar"');
    });

    it('quoteIfSpecialCharacters handles strings with only spaces', () => {
        expect(quoteIfSpecialCharacters(' ')).toBe('');
    });

    it('quoteIfSpecialCharacters handles strings with unicode characters', () => {
        expect(quoteIfSpecialCharacters('фывапр')).toBe('фывапр'); // unicode is not special
    });

    it('quoteIfSpecialCharacters handles strings with emojis', () => {
        expect(quoteIfSpecialCharacters('foo😊bar')).toBe('foo😊bar'); // emoji is not special
    });

    it('quoteIfSpecialCharacters quotes all Lucene special characters', () => {
        const luceneSpecials = ['+', '-', '&&', '||', '!', '(', ')', '{', '}', '[', ']', '^', '"', '~', '*', '?', ':', '\\', '/'];
        for (const char of luceneSpecials) {
            expect(quoteIfSpecialCharacters(char)).toBe(`"${char}"`);
            expect(quoteIfSpecialCharacters(`foo${char}bar`)).toBe(`"foo${char}bar"`);
        }
    });
});
