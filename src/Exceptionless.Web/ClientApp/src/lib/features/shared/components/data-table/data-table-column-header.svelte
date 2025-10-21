<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Column } from '@tanstack/svelte-table';
    import type { HTMLAttributes } from 'svelte/elements';

    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { cn } from '$lib/utils';
    import ArrowDown from '@lucide/svelte/icons/arrow-down';
    import ArrowUp from '@lucide/svelte/icons/arrow-up';
    import ChevronsUpDown from '@lucide/svelte/icons/chevrons-up-down';
    import EyeOff from '@lucide/svelte/icons/eye-off';

    type Props = HTMLAttributes<HTMLDivElement> & {
        column: Column<TData, unknown>;
    };

    let { children, class: className, column, ...restProps }: Props = $props();
</script>

{#if column.getCanSort()}
    <div class={cn('flex items-center space-x-2', className)} {...restProps}>
        <DropdownMenu.Root>
            <DropdownMenu.Trigger>
                {#snippet child({ props })}
                    <Button class="data-[state=open]:bg-accent h-8" variant="ghost" {...props}>
                        {#if children}
                            {@render children()}
                        {/if}
                        {#if column.getIsSorted() === 'desc'}
                            <ArrowDown />
                        {:else if column.getIsSorted() === 'asc'}
                            <ArrowUp />
                        {:else}
                            <ChevronsUpDown />
                        {/if}
                    </Button>
                {/snippet}
            </DropdownMenu.Trigger>
            <DropdownMenu.Content align="start">
                <DropdownMenu.Item onclick={() => column.toggleSorting(false)}><ArrowUp class="text-muted-foreground/70 mr-2 size-3.5" />Asc</DropdownMenu.Item>
                <DropdownMenu.Item onclick={() => column.toggleSorting(true)}
                    ><ArrowDown class="text-muted-foreground/70 mr-2 size-3.5" />Desc</DropdownMenu.Item
                >
                <DropdownMenu.Separator />
                <DropdownMenu.Item onclick={() => column.toggleVisibility(false)}
                    ><EyeOff class="text-muted-foreground/70 mr-2 size-3.5" />Hide</DropdownMenu.Item
                >
            </DropdownMenu.Content>
        </DropdownMenu.Root>
    </div>
{:else if children}
    <div class={className} {...restProps}>
        {@render children()}
    </div>
{/if}
