import { fromDate, parseDate, toCalendarDate } from '@internationalized/date';

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

export function getProductTourUsageParams(range: ProductTourUsageRange, now = new Date()): Record<string, string> {
    if (range.kind === 'days') {
        const start = toCalendarDate(fromDate(now, 'UTC'))
            .subtract({ days: range.days - 1 })
            .toDate('UTC');
        return { start: start.toISOString() };
    }
    if (range.kind === 'history') {
        return {};
    }

    const start = parseDate(`${range.month}-01`);
    const end = start.add({ months: 1 });
    return { end: end.toDate('UTC').toISOString(), start: start.toDate('UTC').toISOString() };
}
