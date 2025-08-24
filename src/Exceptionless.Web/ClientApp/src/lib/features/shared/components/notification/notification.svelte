<script lang="ts" module>
    import type { HTMLAttributes } from 'svelte/elements';

    import { type WithElementRef } from '$lib/utils';
    import { tv, type VariantProps } from 'tailwind-variants';

    export const notificationVariants = tv({
        base: 'relative w-full items-start gap-y-0.5 rounded-lg border border-l-4 px-4 py-3 text-sm has-[>svg]:gap-x-3 [&>svg]:size-4 [&>svg]:translate-y-0.5 [&>svg]:text-current',
        defaultVariants: {
            variant: 'default'
        },
        variants: {
            variant: {
                default: 'border-l-border bg-background text-foreground',
                destructive: 'border-l-red-500 bg-red-50 text-red-900 dark:bg-red-900/30 dark:text-red-200',
                information: 'border-l-blue-500 bg-blue-50 text-blue-900 dark:bg-blue-900/30 dark:text-blue-200',
                success: 'border-l-green-500 bg-green-50 text-green-900 dark:bg-green-900/30 dark:text-green-200',
                warning: 'border-l-yellow-500 bg-yellow-50 text-yellow-900 dark:bg-yellow-900/30 dark:text-yellow-200'
            }
        }
    });

    export type NotificationVariant = VariantProps<typeof notificationVariants>['variant'];

    export type NotificationProps = WithElementRef<HTMLAttributes<HTMLDivElement>> & {
        variant?: NotificationVariant;
    };
</script>

<script lang="ts">
    let { children, class: className, ref = $bindable(null), variant = 'default', ...restProps }: NotificationProps = $props();
</script>

<div bind:this={ref} data-slot="notification" class={[notificationVariants({ variant }), className]} {...restProps} role="alert">
    {@render children?.()}
</div>
