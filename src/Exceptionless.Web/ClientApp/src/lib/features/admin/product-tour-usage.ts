import type { ProductTourActivity, ProductTourUsageInterval } from '$generated/api';

export type ProductTourUsageRange =
    | {
          kind: 'history';
      }
    | {
          kind: 'month';
          month: string;
      };

export function getProductTourActivity(
    activity: ProductTourActivity[],
    interval: ProductTourUsageInterval,
    start: null | string | undefined,
    end: string,
    now = new Date()
): (ProductTourActivity & { date: Date })[] {
    const endDate = new Date(Math.min(new Date(end).getTime(), now.getTime()));
    const firstDate = start ?? activity[0]?.date_utc;
    if (!firstDate) {
        return [];
    }

    const cursor = new Date(firstDate);
    cursor.setUTCHours(0, 0, 0, 0);
    if (interval === 'month') {
        cursor.setUTCDate(1);
    }
    const byDate = new Map(activity.map((period) => [period.date_utc.slice(0, 10), period]));
    const result: (ProductTourActivity & { date: Date })[] = [];
    while (cursor < endDate) {
        const key = cursor.toISOString().slice(0, 10);
        result.push({ completed: 0, dismissed: 0, shown: 0, started: 0, ...byDate.get(key), date: new Date(cursor), date_utc: cursor.toISOString() });
        if (interval === 'month') {
            cursor.setUTCMonth(cursor.getUTCMonth() + 1);
        } else {
            cursor.setUTCDate(cursor.getUTCDate() + 1);
        }
    }
    return result;
}

export function getProductTourUsageParams(range: ProductTourUsageRange): Record<string, boolean | string> {
    if (range.kind === 'history') {
        return { history: true };
    }

    return { month: `${range.month}-01` };
}
