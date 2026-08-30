export type ProductTourUsageRange =
    | {
          kind: 'history';
      }
    | {
          kind: 'month';
          month: string;
      };

export function getOutcomeShare(value: { completed: number; dismissed: number }, outcome: 'completed' | 'dismissed'): null | number {
    return getRate(value[outcome], value.completed + value.dismissed);
}

export function getProductTourUsageParams(range: ProductTourUsageRange): Record<string, boolean | string> {
    if (range.kind === 'history') {
        return { history: true };
    }

    return { month: `${range.month}-01` };
}

export function getRate(numerator: number, denominator: number): null | number {
    return denominator > 0 ? numerator / denominator : null;
}

export function getStartSourceShare(source: { count: number }, started: number): null | number {
    return getRate(source.count, started);
}
