import type { UsageInfo } from '#generated/api.ts';

// Seeded synthetic counts make visual regressions reproducible without customer data.
export function createUsageChartData(seed = 731) {
    let state = seed;
    const random = () => {
        state = (Math.imul(state, 1664525) + 1013904223) >>> 0;
        return state / 2 ** 32;
    };

    return Array.from({ length: 12 }, (_, month) => {
        const organizationTotal = Math.round(500_000 + random() * 4_500_000);
        return {
            blocked: Math.round(random() * 150_000),
            date: new Date(Date.UTC(2025, month, 1)),
            deleted: Math.round(random() * 20_000),
            discarded: Math.round(random() * 100_000),
            limit: 4_000_000,
            organization_total: organizationTotal,
            too_big: Math.round(random() * 10_000),
            total: Math.round(organizationTotal * (0.2 + random() * 0.7))
        } satisfies Omit<UsageInfo, 'date'> & { date: Date; organization_total: number };
    });
}
