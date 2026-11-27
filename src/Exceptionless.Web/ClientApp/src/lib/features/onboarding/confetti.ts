export interface ConfettiPiece {
    color: string;
    delay: number;
    drift: number;
    duration: number;
    height: number;
    left: number;
    rotation: number;
    round: boolean;
    width: number;
}

/** Brand green first, with the chart palette for variety. CSS variables keep light and dark mode in step. */
const COLORS = ['var(--primary)', 'var(--primary)', 'var(--chart-1)', 'var(--chart-2)', 'var(--chart-3)', 'var(--chart-4)'];

export function createConfettiPieces(count: number, random: () => number = Math.random): ConfettiPiece[] {
    return Array.from({ length: count }, (_, index) => ({
        color: COLORS[index % COLORS.length]!,
        delay: Math.round(random() * 700),
        drift: Math.round((random() - 0.5) * 30),
        duration: 2200 + Math.round(random() * 1600),
        height: 6 + Math.round(random() * 8),
        left: Math.round(random() * 1000) / 10,
        rotation: Math.round(360 + random() * 720) * (random() < 0.5 ? -1 : 1),
        round: random() < 0.25,
        width: 5 + Math.round(random() * 5)
    }));
}
