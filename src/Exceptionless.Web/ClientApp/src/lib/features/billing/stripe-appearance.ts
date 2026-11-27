import type { Appearance } from '@stripe/stripe-js';

type ResolveOptions = { alpha?: number; over?: string };

/**
 * Builds a Stripe Elements appearance from the app's theme variables, so the payment form matches light and dark mode.
 * Stripe renders in its own iframe, so the colors are resolved here and passed in as plain values.
 */
export function getStripeAppearance(isDark: boolean): Appearance {
    const resolve = createColorResolver();
    const card = resolve('--card');
    const foreground = resolve('--foreground');
    const mutedForeground = resolve('--muted-foreground');
    const border = resolve('--border');
    const input = resolve('--input');
    const primary = resolve('--primary');
    const ring = resolve('--ring');
    const destructive = resolve('--destructive');

    // Match the app's inputs: transparent in light mode, a 30% input tint over the card in dark mode.
    const inputBackground = isDark ? resolve('--input', { alpha: 0.3, over: '--card' }) : card;
    const focusRing = resolve('--ring', { alpha: 0.5 });

    return {
        rules: {
            '.AccordionItem': {
                backgroundColor: inputBackground,
                border: `1px solid ${input}`,
                boxShadow: 'none'
            },
            '.Block': {
                backgroundColor: inputBackground,
                border: `1px solid ${border}`,
                boxShadow: 'none'
            },
            '.Input': {
                backgroundColor: inputBackground,
                border: `1px solid ${input}`,
                boxShadow: 'none'
            },
            '.Input--invalid': {
                border: `1px solid ${destructive}`,
                boxShadow: 'none'
            },
            '.Input:focus': {
                border: `1px solid ${ring}`,
                boxShadow: `0 0 0 3px ${focusRing}`
            },
            '.Label': {
                color: foreground,
                fontWeight: '500'
            },
            '.PickerItem': {
                backgroundColor: inputBackground,
                border: `1px solid ${input}`,
                boxShadow: 'none'
            },
            '.Tab': {
                backgroundColor: inputBackground,
                border: `1px solid ${input}`,
                boxShadow: 'none'
            },
            '.Tab--selected': {
                border: `1px solid ${primary}`,
                boxShadow: 'none'
            },
            '.Tab:focus': {
                boxShadow: `0 0 0 3px ${focusRing}`
            }
        },
        theme: isDark ? 'night' : 'stripe',
        variables: {
            borderRadius: '6px',
            colorBackground: inputBackground,
            colorDanger: destructive,
            colorIcon: mutedForeground,
            colorPrimary: primary,
            colorText: foreground,
            colorTextPlaceholder: mutedForeground,
            colorTextSecondary: mutedForeground,
            fontFamily: 'ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif',
            fontSizeBase: '14px',
            spacingUnit: '4px'
        }
    };
}

/**
 * Resolves CSS color variables to `rgb()` strings. Stripe only accepts hex, rgb, and hsl colors, and the browser
 * canvas converts any CSS color (including oklch) and blends transparent colors for us.
 */
function createColorResolver(): (variable: string, options?: ResolveOptions) => string {
    const styles = getComputedStyle(document.documentElement);
    const canvas = document.createElement('canvas');
    canvas.width = 1;
    canvas.height = 1;
    const context = canvas.getContext('2d', { willReadFrequently: true });

    return (variable, options = {}) => {
        const value = styles.getPropertyValue(variable).trim();
        if (!context || !value) {
            return value || 'inherit';
        }

        context.clearRect(0, 0, 1, 1);
        context.globalAlpha = 1;
        if (options.over) {
            context.fillStyle = styles.getPropertyValue(options.over).trim();
            context.fillRect(0, 0, 1, 1);
        }

        context.globalAlpha = options.alpha ?? 1;
        context.fillStyle = value;
        context.fillRect(0, 0, 1, 1);

        const [red, green, blue, alpha] = context.getImageData(0, 0, 1, 1).data;
        return alpha === 255 ? `rgb(${red}, ${green}, ${blue})` : `rgba(${red}, ${green}, ${blue}, ${(alpha! / 255).toFixed(2)})`;
    };
}
