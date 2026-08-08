/**
 * Exceptionless email design tokens.
 * Single source of truth — all templates reference these via Tailwind class names.
 */
export const colors = {
    primary: '#5E9A00',
    'primary-action': '#6EBC1A',
    dark: '#2c2c2c',
    muted: '#939393',
    bg: '#f7f7f7',
    alert: '#BB423F',
    'alert-bg': '#f4dede',
    border: '#cbcbcb',
    white: '#fefefe'
} as const;

export const tailwindTheme = {
    theme: {
        extend: {
            colors
        }
    }
} as const;
