import { describe, expect, it } from 'vitest';

import {
    buildEnvironmentRows,
    getDiscardUnlistedWarning,
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
        // Arrange
        const environments = [listed('dev')];

        // Act
        const rows = buildEnvironmentRows(environments, []);
        const missing = buildEnvironmentRows(undefined, []);

        // Assert
        expect(rows.map((row) => row.name)).toEqual(['dev']);
        expect(missing).toEqual([]);
    });
});

describe('getEnvironmentDisplayName', () => {
    it.each([
        ['production', 'Production'],
        ['PRODUCTION', 'Production'],
        ['qa-west', 'qa-west'],
        ['QA-West', 'QA-West']
    ])('shows %s as %s', (name, expected) => {
        // Arrange
        const environments = [listed('Production')];

        // Act
        const displayName = getEnvironmentDisplayName(environments, name);

        // Assert
        expect(displayName).toBe(expected);
    });

    it('returns the name when the project has no list', () => {
        // Arrange
        const name = 'Staging';

        // Act
        const displayName = getEnvironmentDisplayName(undefined, name);

        // Assert
        expect(displayName).toBe(name);
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

        // Act
        const names = getListedEnvironmentNames(projects);
        const limitedNames = getListedEnvironmentNames(projects, ['two']);

        // Assert
        expect([...names]).toEqual([
            ['production', 'Production'],
            ['staging', 'Staging']
        ]);
        expect(limitedNames.get('production')).toBe('PRODUCTION');
    });
});

describe('normalizeEnvironmentName', () => {
    it.each([
        [' Production ', 'Production'],
        ['Staging-EU.2', 'Staging-EU.2'],
        ['dev_local', 'dev_local'],
        ['a'.repeat(64), 'a'.repeat(64)],
        ['qa west', undefined],
        ['prod/eu', undefined],
        ['prod:eu', undefined],
        ['-prod', undefined],
        ['.prod', undefined],
        ['produção', undefined],
        ['', undefined],
        ['   ', undefined],
        [undefined, undefined],
        ['a'.repeat(65), undefined],
        ['prod\tuction', undefined],
        ['prod\nuction', undefined]
    ])('normalizes %j to %j', (value, expected) => {
        // Act
        const normalized = normalizeEnvironmentName(value);

        // Assert
        expect(normalized).toBe(expected);
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
        const missing = splitDiscardedEnvironments(undefined);

        // Assert
        expect(named.map((environment) => environment.key)).toEqual(['prodution', 'dev']);
        expect(unspecified?.count).toBe(9);
        expect(missing).toEqual({ named: [], unspecified: undefined });
    });
});

describe('getDiscardUnlistedWarning', () => {
    const rows = (...counts: (number | undefined)[]) =>
        buildEnvironmentRows(
            counts.map((_, index) => listed(`env-${index}`)),
            counts.flatMap((count, index) => (count === undefined ? [] : [{ count, key: `env-${index}` }]))
        );

    it.each([
        ['no listed environments', [], true, 'empty-list'],
        ['no listed environment has events', [undefined, 0], true, 'no-recent-events'],
        ['a listed environment has events', [undefined, 3], true, undefined],
        ['recent activity has not loaded', [undefined, 3], false, 'unknown-activity']
    ])('returns the right warning when %s', (_, counts, loaded, expected) => {
        // Arrange
        const environmentRows = rows(...(counts as (number | undefined)[]));

        // Act
        const warning = getDiscardUnlistedWarning(environmentRows, loaded);

        // Assert
        expect(warning).toBe(expected);
    });

    it('does not count unlisted environments that have events', () => {
        // Arrange
        const mixed = buildEnvironmentRows([listed('Production')], [{ count: 50, key: 'qa-west' }]);

        // Act
        const warning = getDiscardUnlistedWarning(mixed, true);

        // Assert
        expect(warning).toBe('no-recent-events');
    });
});
