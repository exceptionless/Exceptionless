<script lang="ts" module>
    import type { Snippet } from 'svelte';
    import type { HTMLAttributes } from 'svelte/elements';

    import { type WithElementRef } from '$lib/utils';
    import { tv, type VariantProps } from 'tailwind-variants';

    export const notificationVariants = tv({
        base: 'relative flex w-full items-start gap-3 rounded-lg border px-4 py-3 text-sm',
        defaultVariants: {
            variant: 'default'
        },
        variants: {
            variant: {
                default: 'border-border bg-card text-card-foreground',
                destructive:
                    'border-[#cf222e]/40 bg-[#ffebe9] text-[#1f2328] [&_svg]:text-[#cf222e] dark:border-[#f85149]/55 dark:bg-[#f85149]/12 dark:text-foreground dark:[&_svg]:text-[#ff7b72]',
                impersonation:
                    'border-[#8250df]/40 bg-[#fbefff] text-[#1f2328] [&_svg]:text-[#8250df] dark:border-[#8957e5]/50 dark:bg-[#211637] dark:text-foreground dark:[&_svg]:text-[#d2a8ff]',
                information:
                    'border-[#1a7f37]/40 bg-[#dafbe1] text-[#1f2328] [&_svg]:text-[#1a7f37] dark:border-[#238636]/60 dark:bg-[#12261a] dark:text-foreground dark:[&_svg]:text-[#3fb950]',
                success:
                    'border-[#1a7f37]/40 bg-[#dafbe1] text-[#1f2328] [&_svg]:text-[#1a7f37] dark:border-[#238636]/60 dark:bg-[#12261a] dark:text-foreground dark:[&_svg]:text-[#3fb950]',
                warning:
                    'border-[#9a6700]/40 bg-[#fff8c5] text-[#1f2328] [&_svg]:text-[#9a6700] dark:border-[#9e6a03]/60 dark:bg-[#2b2111] dark:text-foreground dark:[&_svg]:text-[#d29922]'
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
            <div class="flex items-center gap-3">
                {#if icon}
                    <span class="shrink-0 [&>svg]:size-4">
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
