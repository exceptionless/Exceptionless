import type { TransitionConfig } from 'svelte/transition';

import { type ClassValue, clsx } from 'clsx';
import { cubicOut } from 'svelte/easing';
import { twMerge } from 'tailwind-merge';

export const nameof = <T>(name: keyof T) => name;

export function cn(...inputs: ClassValue[]) {
    return twMerge(clsx(inputs));
}

type FlyAndScaleParams = {
    duration?: number;
    start?: number;
    x?: number;
    y?: number;
};

export const flyAndScale = (node: Element, params: FlyAndScaleParams = { duration: 150, start: 0.95, x: 0, y: -8 }): TransitionConfig => {
    const style = getComputedStyle(node);
    const transform = style.transform === 'none' ? '' : style.transform;

    const scaleConversion = (valueA: number, scaleA: [number, number], scaleB: [number, number]) => {
        const [minA, maxA] = scaleA;
        const [minB, maxB] = scaleB;

        const percentage = (valueA - minA) / (maxA - minA);
        const valueB = percentage * (maxB - minB) + minB;

        return valueB;
    };

    const styleToString = (style: Record<string, number | string | undefined>): string => {
        return Object.keys(style).reduce((str, key) => {
            if (style[key] === undefined) return str;
            return str + `${key}:${style[key]};`;
        }, '');
    };

    return {
        css: (t) => {
            const y = scaleConversion(t, [0, 1], [params.y ?? 5, 0]);
            const x = scaleConversion(t, [0, 1], [params.x ?? 0, 0]);
            const scale = scaleConversion(t, [0, 1], [params.start ?? 0.95, 1]);

            return styleToString({
                opacity: t,
                transform: `${transform} translate3d(${x}px, ${y}px, 0) scale(${scale})`
            });
        },
        delay: 0,
        duration: params.duration ?? 200,
        easing: cubicOut
    };
};
