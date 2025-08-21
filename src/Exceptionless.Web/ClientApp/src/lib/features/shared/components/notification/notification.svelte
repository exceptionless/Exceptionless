<script lang="ts" module>
    import { tv, type VariantProps } from 'tailwind-variants';

    export const notificationVariants = tv({
        base: 'relative w-full border-l-4 px-4 py-3 text-sm',
        defaultVariants: {
            variant: 'default'
        },
        variants: {
            variant: {
                default: 'border-l-border bg-background text-foreground',
                destructive: 'border-l-red-500 bg-red-50 text-red-900 dark:bg-red-900/10 dark:text-red-100',
                information: 'border-l-blue-500 bg-blue-50 text-blue-900 dark:bg-blue-900/10 dark:text-blue-100',
                success: 'border-l-green-500 bg-green-50 text-green-900 dark:bg-green-900/10 dark:text-green-100',
                warning: 'border-l-yellow-500 bg-yellow-50 text-yellow-900 dark:bg-yellow-900/10 dark:text-yellow-100'
            }
        }
    });

    export type NotificationVariant = VariantProps<typeof notificationVariants>['variant'];
</script>

<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    import { cn, type WithElementRef } from '$lib/utils';

    let {
        children,
        class: className,
        ref = $bindable(null),
        variant = 'default',
        ...restProps
    }: WithElementRef<HTMLAttributes<HTMLDivElement>> & {
        variant?: NotificationVariant;
    } = $props();
</script>

<div bind:this={ref} data-slot="notification" class={cn(notificationVariants({ variant }), className)} {...restProps} role="alert">
    {@render children?.()}
</div>
