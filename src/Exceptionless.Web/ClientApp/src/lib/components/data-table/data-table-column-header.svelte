<script lang="ts">
    import type { Column } from '@tanstack/svelte-table';
    import IconArrowDownward from '~icons/mdi/arrow-downward';
    import IconArrowUpward from '~icons/mdi/arrow-upward';
    import IconUnfoldMore from '~icons/mdi/unfold-more-horizontal';
    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { cn } from '$lib/utils';

    type TData = $$Generic;
    export let column: Column<TData, unknown>;

    let className: string | undefined | null = undefined;
    export { className as class };

    function handleAscSort(e: Event) {
        if (column.getIsSorted() === 'asc') {
            return;
        }

        column.getToggleSortingHandler()?.(e);
    }

    function handleDescSort(e: Event) {
        if (column.getIsSorted() === 'desc') {
            return;
        }

        column.getToggleSortingHandler()?.(e);
    }
</script>

{#if column.getCanSort()}
    <div class={cn('flex items-center', className)}>
        <DropdownMenu.Root>
            <DropdownMenu.Trigger asChild let:builder>
                <Button variant="ghost" builders={[builder]} class="-ml-3 h-8 data-[state=open]:bg-accent">
                    <slot />
                    {#if column.getIsSorted() === 'desc'}
                        <IconArrowDownward class="ml-2 h-4 w-4" />
                    {:else if column.getIsSorted() === 'asc'}
                        <IconArrowUpward class="ml-2 h-4 w-4" />
                    {:else}
                        <IconUnfoldMore class="ml-2 h-4 w-4" />
                    {/if}
                </Button>
            </DropdownMenu.Trigger>
            <DropdownMenu.Content align="start">
                <DropdownMenu.Item on:click={handleAscSort}>Asc</DropdownMenu.Item>
                <DropdownMenu.Item on:click={handleDescSort}>Desc</DropdownMenu.Item>
            </DropdownMenu.Content>
        </DropdownMenu.Root>
    </div>
{:else}
    <slot />
{/if}
