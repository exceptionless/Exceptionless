import type { AssistantAccess, AssistantAccessState } from './models';

export function resolveAssistantAccessState(
    organizationId: string | undefined,
    access: AssistantAccess | undefined,
    isPending: boolean,
    isError: boolean,
    isFetching: boolean
): AssistantAccessState {
    if (!organizationId) {
        return 'disabled';
    }

    if (access) {
        if (access.enabled !== true) {
            return 'disabled';
        }

        if (access.has_access) {
            return 'available';
        }

        return access.upgrade_required ? 'upgrade-required' : 'disabled';
    }

    if (isPending || (isError && isFetching)) {
        return 'loading';
    }

    return isError ? 'error' : 'disabled';
}
