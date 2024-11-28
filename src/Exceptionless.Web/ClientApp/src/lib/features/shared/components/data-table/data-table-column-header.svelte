<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Column } from '@tanstack/svelte-table';
    import type { HTMLAttributes } from 'svelte/elements';

    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { cn } from '$lib/utils';
    import IconArrowDownward from '~icons/mdi/arrow-downward';
    import IconArrowUpward from '~icons/mdi/arrow-upward';
    import IconEyeOff from '~icons/mdi/eye-off-outline';
    import IconUnfoldMore from '~icons/mdi/unfold-more-horizontal';

    type Props = HTMLAttributes<HTMLDivElement> & {
        column: Column<TData, unknown>;
    };

    let { children, class: className, column, ...restProps }: Props = $props();
</script>

{#if column.getCanSort()}
    <div class={cn('flex items-center', className)} {...restProps}>
        <DropdownMenu.Root>
            <DropdownMenu.Trigger>
                {#snippet child({ props })}
                    <Button class="-ml-3 h-8 data-[state=open]:bg-accent" variant="ghost" {...props}>
                        {#if children}
                            {@render children()}
                        {/if}
                        {#if column.getIsSorted() === 'desc'}
                            <IconArrowDownward />
                        {:else if column.getIsSorted() === 'asc'}
                            <IconArrowUpward />
                        {:else}
                            <IconUnfoldMore />
                        {/if}
                    </Button>
                {/snippet}
            </DropdownMenu.Trigger>
            <DropdownMenu.Content align="start">
                <DropdownMenu.Item onclick={() => column.toggleSorting(false)}
                    ><IconArrowUpward class="mr-2 size-3.5 text-muted-foreground/70" />Asc</DropdownMenu.Item
                >
                <DropdownMenu.Item onclick={() => column.toggleSorting(true)}
                    ><IconArrowDownward class="mr-2 size-3.5 text-muted-foreground/70" />Desc</DropdownMenu.Item
                >
                <DropdownMenu.Separator />
                <DropdownMenu.Item onclick={() => column.toggleVisibility(false)}
                    ><IconEyeOff class="mr-2 size-3.5 text-muted-foreground/70" />Hide</DropdownMenu.Item
                >
            </DropdownMenu.Content>
        </DropdownMenu.Root>
    </div>
{:else if children}
    <div class={className} {...restProps}>
        {@render children()}
    </div>
{/if}
