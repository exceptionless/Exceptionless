import { PersistedState } from 'runed';

export const organization = new PersistedState<string | undefined>('organization', undefined);

export function dispatchSwitchOrganizationEvent() {
    document.dispatchEvent(
        new CustomEvent('switch-organization', {
            bubbles: true,
            detail: organization.current
        })
    );
}
