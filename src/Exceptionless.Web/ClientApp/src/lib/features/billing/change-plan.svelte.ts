export interface ShowChangePlanDialogOptions {
    initialPlanId?: string;
    onSuccess?: () => Promise<void> | void;
}

interface ChangePlanDialogState {
    initialPlanId: string | undefined;
    onSuccess: (() => Promise<void> | void) | undefined;
    open: boolean;
    organizationId: string | undefined;
}

const state: ChangePlanDialogState = $state({
    initialPlanId: undefined,
    onSuccess: undefined,
    open: false,
    organizationId: undefined
});

export const changePlanDialog = {
    get initialPlanId() {
        return state.initialPlanId;
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
        state.initialPlanId = undefined;
        state.onSuccess = undefined;
        state.open = false;
        state.organizationId = undefined;
    }
};

export function showChangePlanDialog(organizationId: string, options: ShowChangePlanDialogOptions = {}): void {
    state.initialPlanId = options.initialPlanId;
    state.onSuccess = options.onSuccess;
    state.organizationId = organizationId;
    state.open = true;
}
