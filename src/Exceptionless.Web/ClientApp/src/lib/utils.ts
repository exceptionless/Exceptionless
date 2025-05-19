import { type ClassValue, clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';

export type WithElementRef<T, U extends HTMLElement = HTMLElement> = T & { ref?: null | U };

export type WithoutChild<T> = T extends { child?: unknown } ? Omit<T, 'child'> : T;
export type WithoutChildren<T> = T extends { children?: unknown } ? Omit<T, 'children'> : T;
export type WithoutChildrenOrChild<T> = WithoutChildren<WithoutChild<T>>;

// TODO: Convert to https://svelte.dev/docs/svelte/class#The-class:-directive
export function cn(...inputs: ClassValue[]) {
    return twMerge(clsx(inputs));
}

export const nameof = <T>(name: keyof T) => name;
