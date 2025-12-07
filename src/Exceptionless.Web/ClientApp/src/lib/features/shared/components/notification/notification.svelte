<script lang="ts" module>
    import type { Snippet } from 'svelte';
    import type { HTMLAttributes } from 'svelte/elements';

    import { type WithElementRef } from '$lib/utils';
    import { tv, type VariantProps } from 'tailwind-variants';

    export const notificationVariants = tv({
        base: 'relative flex w-full items-start gap-3 rounded-lg border border-l-4 px-4 py-3 text-sm',
        defaultVariants: {
            variant: 'default'
        },
        variants: {
            variant: {
                default: 'border-l-border bg-background text-foreground',
                destructive: 'border-l-red-500 bg-red-50 text-red-900 dark:bg-red-900/30 dark:text-red-200',
                impersonation: 'border-l-violet-500 bg-violet-50 text-violet-900 dark:bg-violet-900/30 dark:text-violet-200',
                information: 'border-l-blue-500 bg-blue-50 text-blue-900 dark:bg-blue-900/30 dark:text-blue-200',
                success: 'border-l-green-500 bg-green-50 text-green-900 dark:bg-green-900/30 dark:text-green-200',
                warning: 'border-l-yellow-500 bg-yellow-50 text-yellow-900 dark:bg-yellow-900/30 dark:text-yellow-200'
            }
        }
    });

    export type NotificationVariant = VariantProps<typeof notificationVariants>['variant'];

    export type NotificationProps = WithElementRef<HTMLAttributes<HTMLDivElement>> & {
        action?: Snippet;
        icon?: Snippet;
        variant?: NotificationVariant;
    };
</script>

<script lang="ts">
    let { action, children, class: className, icon, ref = $bindable(null), variant = 'default', ...restProps }: NotificationProps = $props();
</script>

<div bind:this={ref} data-slot="notification" class={[notificationVariants({ variant }), className]} {...restProps} role="alert">
    {#if icon || action}
        <div class="flex w-full flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
            <div class="flex items-start gap-3">
                {#if icon}
                    <span class="mt-0.5 shrink-0 [&>svg]:size-4">
                        {@render icon()}
                    </span>
                {/if}
                <div class="min-w-0 flex-1">
                    {@render children?.()}
                </div>
            </div>
            {#if action}
                <div class="shrink-0 self-start sm:self-center">
                    {@render action()}
                </div>
            {/if}
        </div>
    {:else}
        <div class="min-w-0 flex-1">
            {@render children?.()}
        </div>
    {/if}
</div>
