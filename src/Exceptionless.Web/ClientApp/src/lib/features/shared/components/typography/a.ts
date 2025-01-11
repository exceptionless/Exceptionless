import type { HTMLAnchorAttributes } from 'svelte/elements';

import { tv, type VariantProps } from 'tailwind-variants';

export const variants = tv({
    base: 'underline-offset-4',
    defaultVariants: {
        variant: 'default'
    },
    variants: {
        variant: {
            default: 'underline hover:text-primary',
            ghost: 'no-underline hover:text-foreground',
            navigation: 'text-muted-foreground no-underline hover:underline hover:text-foreground',
            primary: 'text-primary hover:underline',
            secondary: 'text-secondary-foreground hover:underline'
        }
    }
});

export type Props = HTMLAnchorAttributes & {
    variant?: Variant;
};
export type Variant = VariantProps<typeof variants>['variant'];
