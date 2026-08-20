export function getBudgetThresholdEventCount(eventLimit: number, threshold: number): null | number {
    if (!Number.isFinite(eventLimit) || !Number.isInteger(threshold) || eventLimit < 0 || threshold < 1 || threshold > 99) {
        return null;
    }

    return Math.ceil((eventLimit * threshold) / 100);
}

export function parseBudgetThresholds(value: string): number[] {
    return [
        ...new Set(
            value
                .split(',')
                .map((threshold) => threshold.trim())
                .filter(Boolean)
                .map(Number)
                .filter(Number.isFinite)
        )
    ].sort((left, right) => left - right);
}
