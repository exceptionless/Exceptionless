import { type ClassValue, clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';

export type WithElementRef<T, U extends HTMLElement = HTMLElement> = T & { ref?: null | U };

// CUSTOM: KEEP EXCEPTIONLESS-SPECIFIC SVELTE PROPS SHAPE HELPERS UNTIL THE WHOLE APP CAN MIGRATE OFF LEGACY CHILD PROP PATTERNS.
export type WithoutChild<T> = T extends { child?: unknown } ? Omit<T, 'child'> : T;
export type WithoutChildren<T> = T extends { children?: unknown } ? Omit<T, 'children'> : T;
export type WithoutChildrenOrChild<T> = WithoutChildren<WithoutChild<T>>;
export function cn(...inputs: ClassValue[]) {
    // TODO: Migrate this helper callsite-by-callsite to Svelte's class directive so we can drop this adapter. https://svelte.dev/docs/svelte/class#The-class:-directive

    return twMerge(clsx(inputs));
}

export const nameof = <T>(name: keyof T) => name;
