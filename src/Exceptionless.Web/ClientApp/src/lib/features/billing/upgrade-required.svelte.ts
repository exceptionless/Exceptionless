import { ProblemDetails } from '@exceptionless/fetchclient';

interface UpgradeRequiredState {
    message: string;
    open: boolean;
    organizationId: string | undefined;
    retryCallback: (() => Promise<void> | void) | undefined;
}

const state: UpgradeRequiredState = $state({
    message: '',
    open: false,
    organizationId: undefined,
    retryCallback: undefined
});

export const upgradeRequiredDialog = {
    get message() {
        return state.message;
    },
    get open() {
        return state.open;
    },
    set open(value: boolean) {
        state.open = value;
        if (!value) {
            state.retryCallback = undefined;
        }
    },
    get organizationId() {
        return state.organizationId;
    },
    get retryCallback() {
        return state.retryCallback;
    }
};

/**
 * If the error is a 426 Upgrade Required ProblemDetails, opens the billing
 * dialog and optionally wires a retry callback. No-ops for any other error.
 *
 * Returns true if the dialog was opened, false otherwise.
 */
export function showBillingDialogOnUpgradeProblem(error: unknown, organizationId: string | undefined, retryCallback?: () => Promise<void> | void): boolean {
    if (!isUpgradeRequired(error)) {
        return false;
    }

    state.message = error.title || 'Please upgrade your plan to continue.';
    state.organizationId = organizationId;
    state.retryCallback = retryCallback;
    state.open = true;

    return true;
}

/**
 * Checks if a ProblemDetails error represents a 426 Upgrade Required response.
 */
export function isUpgradeRequired(error: unknown): error is ProblemDetails {
    return error instanceof ProblemDetails && error.status === 426;
}

/**
 * Opens the upgrade dialog directly (without a 426 response).
 * Use for proactive upgrade prompts like premium notification settings.
 */
export function showUpgradeDialog(organizationId: string, message?: string): void {
    state.message = message || 'Please upgrade your plan to enable this feature.';
    state.organizationId = organizationId;
    state.retryCallback = undefined;
    state.open = true;
}
