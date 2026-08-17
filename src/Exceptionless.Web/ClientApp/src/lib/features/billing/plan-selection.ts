export function resolveInitialTierId(tierIds: string[], requestedTierId: string | undefined, currentTierId: string, isFreeCurrent: boolean): string {
    if (requestedTierId && tierIds.includes(requestedTierId)) {
        return requestedTierId;
    }

    if (isFreeCurrent) {
        return tierIds[0] ?? '';
    }

    const currentTierIndex = tierIds.indexOf(currentTierId);
    return tierIds[currentTierIndex + 1] ?? currentTierId;
}
