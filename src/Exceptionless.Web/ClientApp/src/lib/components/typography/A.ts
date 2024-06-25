import type { Snippet } from 'svelte';
import type { HTMLAnchorAttributes } from 'svelte/elements';
import { tv, type VariantProps } from 'tailwind-variants';

export const variants = tv({
    base: 'underline-offset-4',
    variants: {
        variant: {
            default: 'underline hover:text-primary',
            navigation: 'text-muted-foreground no-underline hover:underline hover:text-foreground',
            primary: 'text-primary hover:underline',
            secondary: 'text-secondary-foreground hover:underline',
            ghost: 'no-underline hover:text-foreground'
        }
    },
    defaultVariants: {
        variant: 'default'
    }
});

export type Variant = VariantProps<typeof variants>['variant'];
export type Props = HTMLAnchorAttributes & {
    variant?: Variant;
};
