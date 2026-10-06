import { describe, expect, it } from 'vitest';

import { getUtcMonthKey, sortOrganizationsByName } from './utils';

describe('getUtcMonthKey', () => {
    it('changes only when the UTC month changes', () => {
        expect(getUtcMonthKey(new Date('2026-08-01T00:00:00.000Z'))).toBe(getUtcMonthKey(new Date('2026-08-31T23:59:59.999Z')));
        expect(getUtcMonthKey(new Date('2026-09-01T00:00:00.000Z'))).not.toBe(getUtcMonthKey(new Date('2026-08-31T23:59:59.999Z')));
    });
});

describe('sortOrganizationsByName', () => {
    it('SortOrganizationsByName_WithMixedNames_UsesNaturalOrderingAndStableIdsWithoutMutation', () => {
        // Arrange
        const organizations = Object.freeze([
            { id: '8', name: 'Organization 10' },
            { id: '7', name: 'Organization 2' },
            { id: '4', name: 'éclair' },
            { id: '3', name: 'Eclair' },
            { id: '2', name: 'Alpha' },
            { id: '1', name: 'alpha' },
            { id: '6', name: 'Duplicate' },
            { id: '5', name: 'Duplicate' }
        ]);
        const original = [...organizations];

        // Act
        const ascending = sortOrganizationsByName(organizations);
        const descending = sortOrganizationsByName(organizations, true);

        // Assert
        expect(ascending.map((organization) => organization.id)).toEqual(['1', '2', '5', '6', '3', '4', '7', '8']);
        expect(descending).toEqual([...ascending].reverse());
        expect(organizations).toEqual(original);
        expect(ascending).not.toBe(organizations);
    });
});
