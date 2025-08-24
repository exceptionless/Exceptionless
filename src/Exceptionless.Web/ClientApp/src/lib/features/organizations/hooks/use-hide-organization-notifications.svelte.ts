import { showOrganizationNotifications } from '$features/organizations/context.svelte';
import { onDestroy } from 'svelte';

/**
 * Reference counter to track how many components are actively hiding organization notifications.
 *
 * This counter-based approach is necessary because multiple pages/components can be initialized
 * simultaneously (e.g., during navigation transitions or when multiple components mount at once).
 * Without proper reference counting, the notification visibility state could be incorrectly
 * overwritten when one component unmounts while another still needs notifications hidden.
 *
 * For example:
 * 1. Page A mounts and hides notifications (hideCount = 1)
 * 2. Page B mounts and also needs notifications hidden (hideCount = 2)
 * 3. Page A unmounts - without counting, it might show notifications even though Page B still needs them hidden
 * 4. Only when hideCount reaches 0 should notifications be shown again
 */
let _hideCount = 0;

/**
 * Hook to temporarily hide organization notifications while a component is mounted.
 */
export function useHideOrganizationNotifications() {
    _hideCount++;
    showOrganizationNotifications.set(false);

    onDestroy(() => {
        _hideCount = Math.max(0, _hideCount - 1);

        if (_hideCount === 0) {
            showOrganizationNotifications.set(true);
        }
    });
}
