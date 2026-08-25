export const colors = {
    alert: '#BB423F',
    'alert-bg': '#f5e2e2',
    'alert-border': '#6f2725',
    bg: '#f7f7f7',
    border: '#cbcbcb',
    dark: '#2c2c2c',
    muted: '#939393',
    primary: '#5E9A00',
    'primary-action': '#6EBC1A',
    white: '#fefefe'
} as const;

export const tailwindTheme = {
    theme: {
        extend: {
            colors
        }
    }
} as const;
