import { ProblemDetails } from '@exceptionless/fetchclient';

interface UpgradeRequiredState {
    open: boolean;
    message: string;
    organizationId: string | undefined;
}

const state: UpgradeRequiredState = $state({
    open: false,
    message: '',
    organizationId: undefined
});

export const upgradeRequiredDialog = {
    get open() {
        return state.open;
    },
    set open(value: boolean) {
        state.open = value;
    },
    get message() {
        return state.message;
    },
    get organizationId() {
        return state.organizationId;
    }
};

/**
 * Checks if a ProblemDetails error represents a 426 Upgrade Required response.
 */
export function isUpgradeRequired(error: unknown): error is ProblemDetails {
    return error instanceof ProblemDetails && error.status === 426;
}

/**
 * Handles a 426 Upgrade Required response by opening a confirmation dialog
 * matching the legacy UI behavior: shows the backend message with
 * "Upgrade Plan" and "Cancel" buttons.
 *
 * Returns true if the error was a 426 and was handled, false otherwise.
 */
export function handleUpgradeRequired(error: unknown, organizationId: string | undefined): boolean {
    if (!isUpgradeRequired(error)) {
        return false;
    }

    state.message = error.title || 'Please upgrade your plan to continue.';
    state.organizationId = organizationId;
    state.open = true;

    return true;
}
