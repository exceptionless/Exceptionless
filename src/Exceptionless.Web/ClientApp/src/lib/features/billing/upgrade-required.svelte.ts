import { ProblemDetails } from '@foundatiofx/fetchclient';

export interface ShowUpgradeDialogOptions {
    directToPlanPicker?: boolean;
    initialTierId?: string;
    onSuccess?: () => Promise<void> | void;
}

interface UpgradeRequiredState {
    initialTierId: string | undefined;
    message: string;
    onSuccess: (() => Promise<void> | void) | undefined;
    open: boolean;
    organizationId: string | undefined;
    step: 'confirmation' | 'plan-picker';
}

const state: UpgradeRequiredState = $state({
    initialTierId: undefined,
    message: '',
    onSuccess: undefined,
    open: false,
    organizationId: undefined,
    step: 'confirmation'
});

export const upgradeRequiredDialog = {
    get initialTierId() {
        return state.initialTierId;
    },
    get message() {
        return state.message;
    },
    get onSuccess() {
        return state.onSuccess;
    },
    get open() {
        return state.open;
    },
    get organizationId() {
        return state.organizationId;
    },
    reset() {
        state.open = false;
        state.initialTierId = undefined;
        state.message = '';
        state.onSuccess = undefined;
        state.organizationId = undefined;
        state.step = 'confirmation';
    },
    showPlanPicker() {
        state.step = 'plan-picker';
    },
    get step() {
        return state.step;
    }
};

export function isUpgradeRequired(error: unknown): error is ProblemDetails {
    return error instanceof ProblemDetails && error.status === 426;
}

export function showBillingDialogOnUpgradeProblem(error: unknown, organizationId: string | undefined, retryCallback?: () => Promise<void> | void): boolean {
    if (!isUpgradeRequired(error)) {
        return false;
    }

    openUpgradeDialog({
        message: error.title || 'Please upgrade your plan to continue.',
        onSuccess: retryCallback,
        organizationId,
        step: 'confirmation'
    });

    return true;
}

export function showUpgradeDialog(organizationId: string, message?: string, options: ShowUpgradeDialogOptions = {}): void {
    openUpgradeDialog({
        initialTierId: options.initialTierId,
        message: message || 'Please upgrade your plan to enable this feature.',
        onSuccess: options.onSuccess,
        organizationId,
        step: options.directToPlanPicker ? 'plan-picker' : 'confirmation'
    });
}

function openUpgradeDialog(options: {
    initialTierId?: string;
    message: string;
    onSuccess?: () => Promise<void> | void;
    organizationId: string | undefined;
    step: UpgradeRequiredState['step'];
}): void {
    state.initialTierId = options.initialTierId;
    state.message = options.message;
    state.onSuccess = options.onSuccess;
    state.organizationId = options.organizationId;
    state.step = options.step;
    state.open = true;
}
