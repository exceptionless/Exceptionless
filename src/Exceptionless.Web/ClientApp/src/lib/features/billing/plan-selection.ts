const YEARLY_SUFFIX = '_YEARLY';

export function resolveInitialPlanTierId(tierIds: string[], initialPlanId: string | undefined, currentTierId: string, isFreeCurrent: boolean): string {
    const initialTierId = initialPlanId?.endsWith(YEARLY_SUFFIX) ? initialPlanId.slice(0, -YEARLY_SUFFIX.length) : initialPlanId;

    if (initialTierId && tierIds.includes(initialTierId)) {
        return initialTierId;
    }

    if (isFreeCurrent) {
        return tierIds[0] ?? '';
    }

    const currentTierIndex = tierIds.indexOf(currentTierId);
    return tierIds[currentTierIndex + 1] ?? currentTierId;
}
