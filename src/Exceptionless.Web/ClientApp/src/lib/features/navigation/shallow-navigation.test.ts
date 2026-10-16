import type { NavigationBase, NavigationTarget } from '$app/navigation';

import { describe, expect, it } from 'vitest';

import { isShallowNavigationWithinPage } from './shallow-navigation';

function target(routeId: string): NavigationTarget {
    return { params: {}, route: { id: routeId }, scroll: null, url: new URL(`http://localhost${routeId}`) } as NavigationTarget;
}

describe('isShallowNavigationWithinPage', () => {
    it.each<{ expected: boolean; name: string; navigation: Pick<NavigationBase, 'from' | 'shallow' | 'to'> }>([
        { expected: true, name: 'a shallow navigation on the same route', navigation: { from: target('/sessions'), shallow: true, to: target('/sessions') } },
        { expected: false, name: 'Back or Forward into another route', navigation: { from: target('/stream'), shallow: true, to: target('/sessions') } },
        { expected: false, name: 'a full navigation on the same route', navigation: { from: target('/sessions'), shallow: false, to: target('/sessions') } },
        { expected: false, name: 'the first navigation', navigation: { from: null, shallow: false, to: target('/sessions') } }
    ])('returns $expected for $name', ({ expected, navigation }) => {
        // Act
        const result = isShallowNavigationWithinPage(navigation);

        // Assert
        expect(result).toBe(expected);
    });
});
