import { describe, expect, it } from 'vitest';

import { deserializeFilters, quoteIfSpecialCharacters, serializeFilters } from './helpers.svelte';
import {
    BooleanFilter,
    DateFilter,
    KeywordFilter,
    LevelFilter,
    NumberFilter,
    ProjectFilter,
    ReferenceFilter,
    SessionFilter,
    StatusFilter,
    StringFilter,
    TagFilter,
    TypeFilter,
    VersionFilter
} from './models.svelte';

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

describe('serializeFilters', () => {
    it('serializes an empty array', () => {
        expect(serializeFilters([])).toBe('[]');
    });

    it('serializes a BooleanFilter with term and value', () => {
        const filters = [new BooleanFilter('is_fixed', true)];
        const result = JSON.parse(serializeFilters(filters));

        expect(result).toHaveLength(1);
        expect(result[0]).toEqual({ term: 'is_fixed', type: 'boolean', value: true });
    });

    it('serializes a DateFilter with term and string value', () => {
        const filters = [new DateFilter('date', '2024-01-01')];
        const result = JSON.parse(serializeFilters(filters));

        expect(result[0]).toEqual({ term: 'date', type: 'date', value: '2024-01-01' });
    });

    it('serializes a KeywordFilter with value', () => {
        const filters = [new KeywordFilter('status:open')];
        const result = JSON.parse(serializeFilters(filters));

        expect(result[0]).toEqual({ type: 'keyword', value: 'status:open' });
    });

    it('serializes a LevelFilter with multiple values', () => {
        const filters = [new LevelFilter(['Error', 'Fatal'] as never[])];
        const result = JSON.parse(serializeFilters(filters));

        expect(result[0]).toEqual({ type: 'level', value: ['Error', 'Fatal'] });
    });

    it('serializes a NumberFilter with term and value', () => {
        const filters = [new NumberFilter('value', 42)];
        const result = JSON.parse(serializeFilters(filters));

        expect(result[0]).toEqual({ term: 'value', type: 'number', value: 42 });
    });

    it('serializes a ProjectFilter with multiple values', () => {
        const filters = [new ProjectFilter(['proj1', 'proj2'])];
        const result = JSON.parse(serializeFilters(filters));

        expect(result[0]).toEqual({ type: 'project', value: ['proj1', 'proj2'] });
    });

    it('serializes a ReferenceFilter with value', () => {
        const filters = [new ReferenceFilter('ref-123')];
        const result = JSON.parse(serializeFilters(filters));

        expect(result[0]).toEqual({ type: 'reference', value: 'ref-123' });
    });

    it('serializes a SessionFilter with value', () => {
        const filters = [new SessionFilter('session-abc')];
        const result = JSON.parse(serializeFilters(filters));

        expect(result[0]).toEqual({ type: 'session', value: 'session-abc' });
    });

    it('serializes a StatusFilter with multiple values', () => {
        const filters = [new StatusFilter(['open', 'regressed'] as never[])];
        const result = JSON.parse(serializeFilters(filters));

        expect(result[0]).toEqual({ type: 'status', value: ['open', 'regressed'] });
    });

    it('serializes a StringFilter with term and value', () => {
        const filters = [new StringFilter('error.message', 'null ref')];
        const result = JSON.parse(serializeFilters(filters));

        expect(result[0]).toEqual({ term: 'error.message', type: 'string', value: 'null ref' });
    });

    it('serializes a TagFilter with values', () => {
        const filters = [new TagFilter(['error', 'log'] as never[])];
        const result = JSON.parse(serializeFilters(filters));

        expect(result[0]).toEqual({ type: 'tag', value: ['error', 'log'] });
    });

    it('serializes a TypeFilter with values', () => {
        const filters = [new TypeFilter(['error', 'log'] as never[])];
        const result = JSON.parse(serializeFilters(filters));

        expect(result[0]).toEqual({ type: 'type', value: ['error', 'log'] });
    });

    it('serializes a VersionFilter with term and value', () => {
        const filters = [new VersionFilter('version', '1.2.3')];
        const result = JSON.parse(serializeFilters(filters));

        expect(result[0]).toEqual({ term: 'version', type: 'version', value: '1.2.3' });
    });

    it('serializes filters without optional term or value', () => {
        const filters = [new BooleanFilter()];
        const result = JSON.parse(serializeFilters(filters));

        expect(result[0]).toEqual({ type: 'boolean' });
    });

    it('serializes multiple filters', () => {
        const filters = [new KeywordFilter('error'), new StatusFilter(['open'] as never[]), new BooleanFilter('is_fixed', false)];
        const result = JSON.parse(serializeFilters(filters));

        expect(result).toHaveLength(3);
        expect(result[0].type).toBe('keyword');
        expect(result[1].type).toBe('status');
        expect(result[2].type).toBe('boolean');
    });
});

describe('deserializeFilters', () => {
    it('deserializes an empty array', () => {
        expect(deserializeFilters('[]')).toEqual([]);
    });

    it('deserializes a BooleanFilter', () => {
        const filters = deserializeFilters('[{"type":"boolean","term":"is_fixed","value":true}]');

        expect(filters).toHaveLength(1);
        expect(filters[0]).toBeInstanceOf(BooleanFilter);
        expect((filters[0] as BooleanFilter).term).toBe('is_fixed');
        expect((filters[0] as BooleanFilter).value).toBe(true);
    });

    it('deserializes a DateFilter', () => {
        const filters = deserializeFilters('[{"type":"date","term":"date","value":"2024-01-01"}]');

        expect(filters).toHaveLength(1);
        expect(filters[0]).toBeInstanceOf(DateFilter);
        expect((filters[0] as DateFilter).term).toBe('date');
        expect((filters[0] as DateFilter).value).toBe('2024-01-01');
    });

    it('deserializes a KeywordFilter', () => {
        const filters = deserializeFilters('[{"type":"keyword","value":"status:open"}]');

        expect(filters).toHaveLength(1);
        expect(filters[0]).toBeInstanceOf(KeywordFilter);
        expect((filters[0] as KeywordFilter).value).toBe('status:open');
    });

    it('deserializes a LevelFilter', () => {
        const filters = deserializeFilters('[{"type":"level","value":["Error","Fatal"]}]');

        expect(filters).toHaveLength(1);
        expect(filters[0]).toBeInstanceOf(LevelFilter);
        expect((filters[0] as LevelFilter).value).toEqual(['Error', 'Fatal']);
    });

    it('deserializes a NumberFilter', () => {
        const filters = deserializeFilters('[{"type":"number","term":"value","value":42}]');

        expect(filters).toHaveLength(1);
        expect(filters[0]).toBeInstanceOf(NumberFilter);
        expect((filters[0] as NumberFilter).term).toBe('value');
        expect((filters[0] as NumberFilter).value).toBe(42);
    });

    it('deserializes a ProjectFilter', () => {
        const filters = deserializeFilters('[{"type":"project","value":["p1","p2"]}]');

        expect(filters).toHaveLength(1);
        expect(filters[0]).toBeInstanceOf(ProjectFilter);
        expect((filters[0] as ProjectFilter).value).toEqual(['p1', 'p2']);
    });

    it('deserializes a ReferenceFilter', () => {
        const filters = deserializeFilters('[{"type":"reference","value":"ref-123"}]');

        expect(filters).toHaveLength(1);
        expect(filters[0]).toBeInstanceOf(ReferenceFilter);
        expect((filters[0] as ReferenceFilter).value).toBe('ref-123');
    });

    it('deserializes a SessionFilter', () => {
        const filters = deserializeFilters('[{"type":"session","value":"session-abc"}]');

        expect(filters).toHaveLength(1);
        expect(filters[0]).toBeInstanceOf(SessionFilter);
        expect((filters[0] as SessionFilter).value).toBe('session-abc');
    });

    it('deserializes a StatusFilter', () => {
        const filters = deserializeFilters('[{"type":"status","value":["open","regressed"]}]');

        expect(filters).toHaveLength(1);
        expect(filters[0]).toBeInstanceOf(StatusFilter);
        expect((filters[0] as StatusFilter).value).toEqual(['open', 'regressed']);
    });

    it('deserializes a StringFilter', () => {
        const filters = deserializeFilters('[{"type":"string","term":"error.message","value":"null ref"}]');

        expect(filters).toHaveLength(1);
        expect(filters[0]).toBeInstanceOf(StringFilter);
        expect((filters[0] as StringFilter).term).toBe('error.message');
        expect((filters[0] as StringFilter).value).toBe('null ref');
    });

    it('deserializes a TagFilter', () => {
        const filters = deserializeFilters('[{"type":"tag","value":["error","log"]}]');

        expect(filters).toHaveLength(1);
        expect(filters[0]).toBeInstanceOf(TagFilter);
        expect((filters[0] as TagFilter).value).toEqual(['error', 'log']);
    });

    it('deserializes a TypeFilter', () => {
        const filters = deserializeFilters('[{"type":"type","value":["error","log"]}]');

        expect(filters).toHaveLength(1);
        expect(filters[0]).toBeInstanceOf(TypeFilter);
        expect((filters[0] as TypeFilter).value).toEqual(['error', 'log']);
    });

    it('deserializes a VersionFilter', () => {
        const filters = deserializeFilters('[{"type":"version","term":"version","value":"1.2.3"}]');

        expect(filters).toHaveLength(1);
        expect(filters[0]).toBeInstanceOf(VersionFilter);
        expect((filters[0] as VersionFilter).term).toBe('version');
        expect((filters[0] as VersionFilter).value).toBe('1.2.3');
    });

    it('skips unknown filter types', () => {
        const filters = deserializeFilters('[{"type":"unknown","value":"test"},{"type":"keyword","value":"valid"}]');

        expect(filters).toHaveLength(1);
        expect(filters[0]).toBeInstanceOf(KeywordFilter);
    });
});

describe('round-trip serialization', () => {
    it('round-trips a BooleanFilter', () => {
        const original = [new BooleanFilter('is_fixed', true)];
        const result = deserializeFilters(serializeFilters(original));

        expect(result).toHaveLength(1);
        expect(result[0]).toBeInstanceOf(BooleanFilter);
        expect((result[0] as BooleanFilter).term).toBe('is_fixed');
        expect((result[0] as BooleanFilter).value).toBe(true);
    });

    it('round-trips a DateFilter with string value', () => {
        const original = [new DateFilter('created_utc', '2024-06-15T00:00:00Z')];
        const result = deserializeFilters(serializeFilters(original));

        expect(result).toHaveLength(1);
        expect((result[0] as DateFilter).term).toBe('created_utc');
        expect((result[0] as DateFilter).value).toBe('2024-06-15T00:00:00Z');
    });

    it('round-trips a KeywordFilter', () => {
        const original = [new KeywordFilter('status:open OR status:regressed')];
        const result = deserializeFilters(serializeFilters(original));

        expect(result).toHaveLength(1);
        expect((result[0] as KeywordFilter).value).toBe('status:open OR status:regressed');
    });

    it('round-trips a LevelFilter with multiple levels', () => {
        const original = [new LevelFilter(['Error', 'Warning', 'Fatal'] as never[])];
        const result = deserializeFilters(serializeFilters(original));

        expect(result).toHaveLength(1);
        expect((result[0] as LevelFilter).value).toEqual(['Error', 'Warning', 'Fatal']);
    });

    it('round-trips a NumberFilter', () => {
        const original = [new NumberFilter('count', 99)];
        const result = deserializeFilters(serializeFilters(original));

        expect(result).toHaveLength(1);
        expect((result[0] as NumberFilter).term).toBe('count');
        expect((result[0] as NumberFilter).value).toBe(99);
    });

    it('round-trips a ProjectFilter', () => {
        const original = [new ProjectFilter(['abc123', 'def456'])];
        const result = deserializeFilters(serializeFilters(original));

        expect(result).toHaveLength(1);
        expect((result[0] as ProjectFilter).value).toEqual(['abc123', 'def456']);
    });

    it('round-trips a ReferenceFilter', () => {
        const original = [new ReferenceFilter('ref-xyz')];
        const result = deserializeFilters(serializeFilters(original));

        expect(result).toHaveLength(1);
        expect((result[0] as ReferenceFilter).value).toBe('ref-xyz');
    });

    it('round-trips a SessionFilter', () => {
        const original = [new SessionFilter('sess-001')];
        const result = deserializeFilters(serializeFilters(original));

        expect(result).toHaveLength(1);
        expect((result[0] as SessionFilter).value).toBe('sess-001');
    });

    it('round-trips a StatusFilter', () => {
        const original = [new StatusFilter(['open', 'regressed'] as never[])];
        const result = deserializeFilters(serializeFilters(original));

        expect(result).toHaveLength(1);
        expect((result[0] as StatusFilter).value).toEqual(['open', 'regressed']);
    });

    it('round-trips a StringFilter', () => {
        const original = [new StringFilter('error.type', 'NullReferenceException')];
        const result = deserializeFilters(serializeFilters(original));

        expect(result).toHaveLength(1);
        expect((result[0] as StringFilter).term).toBe('error.type');
        expect((result[0] as StringFilter).value).toBe('NullReferenceException');
    });

    it('round-trips a TagFilter', () => {
        const original = [new TagFilter(['Critical', 'UI'] as never[])];
        const result = deserializeFilters(serializeFilters(original));

        expect(result).toHaveLength(1);
        expect((result[0] as TagFilter).value).toEqual(['Critical', 'UI']);
    });

    it('round-trips a TypeFilter', () => {
        const original = [new TypeFilter(['error', 'session'] as never[])];
        const result = deserializeFilters(serializeFilters(original));

        expect(result).toHaveLength(1);
        expect((result[0] as TypeFilter).value).toEqual(['error', 'session']);
    });

    it('round-trips a VersionFilter', () => {
        const original = [new VersionFilter('version', '2.0.0-beta')];
        const result = deserializeFilters(serializeFilters(original));

        expect(result).toHaveLength(1);
        expect((result[0] as VersionFilter).term).toBe('version');
        expect((result[0] as VersionFilter).value).toBe('2.0.0-beta');
    });

    it('round-trips a complex mix of filters', () => {
        const original = [
            new KeywordFilter('error'),
            new StatusFilter(['open'] as never[]),
            new BooleanFilter('is_fixed', false),
            new NumberFilter('value', 10),
            new StringFilter('error.message', 'Connection timeout'),
            new ProjectFilter(['proj-a']),
            new VersionFilter('version', '3.1.0')
        ];
        const result = deserializeFilters(serializeFilters(original));

        expect(result).toHaveLength(7);
        expect(result[0]).toBeInstanceOf(KeywordFilter);
        expect(result[1]).toBeInstanceOf(StatusFilter);
        expect(result[2]).toBeInstanceOf(BooleanFilter);
        expect(result[3]).toBeInstanceOf(NumberFilter);
        expect(result[4]).toBeInstanceOf(StringFilter);
        expect(result[5]).toBeInstanceOf(ProjectFilter);
        expect(result[6]).toBeInstanceOf(VersionFilter);
    });

    it('round-trips filters with undefined values', () => {
        const original = [new BooleanFilter('is_fixed'), new StringFilter('term')];
        const result = deserializeFilters(serializeFilters(original));

        expect(result).toHaveLength(2);
        expect((result[0] as BooleanFilter).term).toBe('is_fixed');
        expect((result[0] as BooleanFilter).value).toBeUndefined();
        expect((result[1] as StringFilter).term).toBe('term');
        expect((result[1] as StringFilter).value).toBeUndefined();
    });
});

describe('defensive deserialization', () => {
    it('returns empty array for invalid JSON', () => {
        expect(deserializeFilters('not json')).toEqual([]);
    });

    it('returns empty array for null input', () => {
        expect(deserializeFilters(null as unknown as string)).toEqual([]);
    });

    it('returns empty array for undefined input', () => {
        expect(deserializeFilters(undefined as unknown as string)).toEqual([]);
    });

    it('returns empty array for empty string', () => {
        expect(deserializeFilters('')).toEqual([]);
    });

    it('returns empty array for JSON object (not array)', () => {
        expect(deserializeFilters('{"type":"keyword","value":"test"}')).toEqual([]);
    });

    it('returns empty array for JSON number', () => {
        expect(deserializeFilters('42')).toEqual([]);
    });

    it('handles missing type field gracefully', () => {
        const result = deserializeFilters('[{"value":"test"}]');

        expect(result).toEqual([]);
    });

    it('handles missing value field gracefully', () => {
        const result = deserializeFilters('[{"type":"keyword"}]');

        expect(result).toHaveLength(1);
        expect(result[0]).toBeInstanceOf(KeywordFilter);
    });

    it('handles XSS payload in value without crashing', () => {
        const xss = '<script>alert(1)</script>';
        const result = deserializeFilters(JSON.stringify([{ type: 'keyword', value: xss }]));

        expect(result).toHaveLength(1);
        expect((result[0] as KeywordFilter).value).toBe(xss);
    });
});
