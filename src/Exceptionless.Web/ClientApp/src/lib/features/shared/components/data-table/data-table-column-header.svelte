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
    import IconUnfoldMore from '~icons/mdi/unfold-more-horizontal';

    type Props = {
        column: Column<TData, unknown>;
    } & HTMLAttributes<Element>;

    let { children, class: className, column }: Props = $props();

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
            <DropdownMenu.Trigger asChild>
                {#snippet children({ builder })}
                    <Button builders={[builder]} class="-ml-3 h-8 data-[state=open]:bg-accent" variant="ghost">
                        {#if children}
                            {@render children()}
                        {/if}
                        {#if column.getIsSorted() === 'desc'}
                            <IconArrowDownward class="ml-2 h-4 w-4" />
                        {:else if column.getIsSorted() === 'asc'}
                            <IconArrowUpward class="ml-2 h-4 w-4" />
                        {:else}
                            <IconUnfoldMore class="ml-2 h-4 w-4" />
                        {/if}
                    </Button>
                {/snippet}
            </DropdownMenu.Trigger>
            <DropdownMenu.Content align="start">
                <DropdownMenu.Item onclick={handleAscSort}>Asc</DropdownMenu.Item>
                <DropdownMenu.Item onclick={handleDescSort}>Desc</DropdownMenu.Item>
            </DropdownMenu.Content>
        </DropdownMenu.Root>
    </div>
{:else if children}
    {@render children()}
{/if}
