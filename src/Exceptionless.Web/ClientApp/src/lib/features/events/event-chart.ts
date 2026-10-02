import type { EventChart, EventChartPoint, EventChartResult } from '$generated/api';

import { EventChartSchema } from '$generated/schemas';

export const eventChartSchema = EventChartSchema.superRefine((chart, context) => {
    if (!chart.measurement && (chart.aggregation !== 'count' || chart.mode === 'events')) {
        context.addIssue({ code: 'custom', message: 'Select a measurement, or use bucketed event counts.', path: ['measurement'] });
    }
    if (!!chart.measurement !== !!chart.unit) {
        context.addIssue({ code: 'custom', message: 'Select a measurement and its unit together.', path: ['unit'] });
    }
});

export type MeasurementPoint = Omit<EventChartPoint, 'date'> & { date: Date };

export function chartSeries(result: EventChartResult | undefined) {
    return (result?.series ?? []).map((series, index) => {
        const key = `series${index}`;
        return {
            color: `var(--chart-${(index % 5) + 1})`,
            data: series.points.map((point) => ({ ...point, date: new Date(point.date), [key]: point.value })),
            key,
            label: series.name,
            value: key
        };
    });
}

export function chartSignature(chart: EventChart | null | undefined): string {
    return JSON.stringify(chart ? [chart.measurement ?? null, chart.unit ?? null, chart.aggregation, chart.group_by ?? null, chart.mode, chart.display] : null);
}

export function formatMeasurement(value: null | number | undefined, unit: string): string {
    if (value == null || !Number.isFinite(value)) {
        return '—';
    }
    const number = new Intl.NumberFormat(undefined, { maximumSignificantDigits: 6 }).format(value);
    return unit === '1' ? number : `${number} ${unit}`;
}

export function measurementUnit(chart: EventChart): string {
    return chart.mode !== 'events' && chart.aggregation === 'count' ? '1' : (chart.unit ?? '1');
}

export function parseChartParameter(value: null | string | undefined): EventChart | null | undefined {
    if (!value) {
        return undefined;
    }
    try {
        const parsed: unknown = JSON.parse(value);
        if (parsed === null) {
            return null;
        }
        const result = eventChartSchema.safeParse(parsed);
        return result.success ? result.data : undefined;
    } catch {
        return undefined;
    }
}
