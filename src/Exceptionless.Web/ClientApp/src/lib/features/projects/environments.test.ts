import { describe, expect, it } from 'vitest';

import {
    buildEnvironmentRows,
    getEnvironmentDisplayName,
    getListedEnvironmentNames,
    normalizeEnvironmentName,
    splitDiscardedEnvironments
} from './environments';

const listed = (name: string, notify = true) => ({ created_utc: '2026-11-01T00:00:00Z', name, notify });

describe('buildEnvironmentRows', () => {
    it('merges listed and observed environments case-insensitively and keeps the listed casing', () => {
        // Arrange
        const environments = [listed('Staging', false), listed('Production')];
        const observed = [
            { count: 120, key: 'production', lastSeen: '2026-11-02T10:00:00Z' },
            { count: 3, key: 'prodution', lastSeen: '2026-11-02T09:00:00Z' },
            { count: 40, key: 'qa-west' }
        ];

        // Act
        const rows = buildEnvironmentRows(environments, observed);

        // Assert
        expect(rows.map((row) => row.name)).toEqual(['Production', 'Staging', 'qa-west', 'prodution']);
        expect(rows[0]).toMatchObject({ count: 120, isListed: true, key: 'production', lastSeen: '2026-11-02T10:00:00Z', notify: true });
        expect(rows[1]).toMatchObject({ count: 0, isListed: true, lastSeen: undefined, notify: false });
        expect(rows[2]).toMatchObject({ count: 40, isListed: false });
        expect(rows[2]!.notify).toBeUndefined();
    });

    it('lists environments that have no events yet and handles missing lists', () => {
        // Act & Assert
        expect(buildEnvironmentRows([listed('dev')], []).map((row) => row.name)).toEqual(['dev']);
        expect(buildEnvironmentRows(undefined, [])).toEqual([]);
    });
});

describe('getEnvironmentDisplayName', () => {
    it.each([
        ['production', 'Production'],
        ['PRODUCTION', 'Production'],
        ['qa-west', 'qa-west'],
        ['QA-West', 'QA-West']
    ])('shows %s as %s', (name, expected) => {
        expect(getEnvironmentDisplayName([listed('Production')], name)).toBe(expected);
    });

    it('returns the name when the project has no list', () => {
        expect(getEnvironmentDisplayName(undefined, 'Staging')).toBe('Staging');
    });
});

describe('getListedEnvironmentNames', () => {
    it('uses the first project to decide the casing and can be limited to specific projects', () => {
        // Arrange
        const projects = [
            { environments: [listed('Production')], id: 'one' },
            { environments: [listed('PRODUCTION'), listed('Staging')], id: 'two' },
            { environments: undefined, id: 'three' }
        ];

        // Act & Assert
        expect([...getListedEnvironmentNames(projects)]).toEqual([
            ['production', 'Production'],
            ['staging', 'Staging']
        ]);
        expect(getListedEnvironmentNames(projects, ['two']).get('production')).toBe('PRODUCTION');
    });
});

describe('normalizeEnvironmentName', () => {
    it.each([
        [' Production ', 'Production'],
        ['qa west', 'qa west'],
        ['a'.repeat(64), 'a'.repeat(64)],
        ['', undefined],
        ['   ', undefined],
        [undefined, undefined],
        ['a'.repeat(65), undefined],
        ['prod\tuction', undefined],
        ['prod\nuction', undefined]
    ])('normalizes %j to %j', (value, expected) => {
        expect(normalizeEnvironmentName(value)).toBe(expected);
    });
});

describe('splitDiscardedEnvironments', () => {
    it('separates the bucket without an environment and orders names by count', () => {
        // Arrange
        const discarded = [
            { count: 2, key: 'dev', last_seen_utc: '2026-11-02T00:00:00Z', name: 'dev' },
            { count: 9, last_seen_utc: '2026-11-02T00:00:00Z' },
            { count: 7, key: 'prodution', last_seen_utc: '2026-11-02T00:00:00Z', name: 'Prodution' }
        ];

        // Act
        const { named, unspecified } = splitDiscardedEnvironments(discarded);

        // Assert
        expect(named.map((environment) => environment.key)).toEqual(['prodution', 'dev']);
        expect(unspecified?.count).toBe(9);
        expect(splitDiscardedEnvironments(undefined)).toEqual({ named: [], unspecified: undefined });
    });
});
