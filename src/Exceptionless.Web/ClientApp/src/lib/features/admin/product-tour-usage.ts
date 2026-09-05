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
        .filter(
            (period) =>
                (!startDate || period.date >= startDate || period.shown + period.started + period.completed + period.dismissed > 0) && period.date < endDate
        );
}

export function getProductTourUsageParams(range: ProductTourUsageRange, now = new Date()): Record<string, string> {
    if (range.kind === 'days') {
        const start = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() + 1 - range.days));
        return { start: start.toISOString() };
    }
    if (range.kind === 'history') {
        return {};
    }

    const start = new Date(`${range.month}-01T00:00:00Z`);
    const end = new Date(Date.UTC(start.getUTCFullYear(), start.getUTCMonth() + 1));
    return { end: end.toISOString(), start: start.toISOString() };
}
