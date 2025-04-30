import { type ClassValue, clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';

// TODO: Convert to https://svelte.dev/docs/svelte/class#The-class:-directive
export function cn(...inputs: ClassValue[]) {
    return twMerge(clsx(inputs));
}

export const nameof = <T>(name: keyof T) => name;
