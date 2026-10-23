import type { NavigationBase } from '$app/navigation';

/**
 * Whether a navigation stays on the page that is already rendered. SvelteKit runs the
 * navigation hooks with `shallow: true` for a shallow `goto` and for Back or Forward to a
 * shallow history entry, such as a query parameter update. Both keep the same page mounted
 * unless Back or Forward crosses into another route's entry.
 */
export function isShallowNavigationWithinPage({ from, shallow, to }: Pick<NavigationBase, 'from' | 'shallow' | 'to'>): boolean {
    return shallow && from?.route.id === to?.route.id;
}
