import type { ProductTourActivity } from '$generated/api';

export type ProductTourUsageRange =
    | {
          days: number;
          kind: 'days';
      }
    | {
          kind: 'history';
      }
    | {
          kind: 'month';
          month: string;
      };

export function getProductTourActivity(
    activity: ProductTourActivity[],
    start: null | string | undefined,
    end: string,
    now = new Date()
): (ProductTourActivity & { date: Date })[] {
    const endDate = new Date(Math.min(new Date(end).getTime(), now.getTime()));
    const startDate = start ? new Date(start) : undefined;
    return activity
        .map((period) => ({ ...period, date: new Date(period.date_utc) }))
        .filter((period) => (!startDate || period.date >= startDate) && period.date < endDate);
}

export function getProductTourUsageParams(range: ProductTourUsageRange): Record<string, boolean | number | string> {
    if (range.kind === 'days') {
        return { days: range.days };
    }
    if (range.kind === 'history') {
        return { history: true };
    }

    return { month: `${range.month}-01` };
}
