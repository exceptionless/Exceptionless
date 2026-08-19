import { ProblemDetails } from '@foundatiofx/fetchclient';

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
    get organizationId() {
        return state.organizationId;
    },
    reset() {
        state.message = '';
        state.open = false;
        state.organizationId = undefined;
        state.retryCallback = undefined;
    },
    get retryCallback() {
        return state.retryCallback;
    }
};

export function isUpgradeRequired(error: unknown): error is ProblemDetails {
    return error instanceof ProblemDetails && error.status === 426;
}

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

export function showUpgradeDialog(organizationId: string, message?: string): void {
    state.message = message || 'Please upgrade your plan to enable this feature.';
    state.organizationId = organizationId;
    state.retryCallback = undefined;
    state.open = true;
}
