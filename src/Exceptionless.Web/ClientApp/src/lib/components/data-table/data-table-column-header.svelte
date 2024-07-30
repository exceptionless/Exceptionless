<script lang="ts" context="module">
    type TData = unknown;
</script>

<script lang="ts" generics="TData">
    import type { HTMLAttributes } from 'svelte/elements';

    import type { Column } from '@tanstack/svelte-table';
    import IconArrowDownward from '~icons/mdi/arrow-downward';
    import IconArrowUpward from '~icons/mdi/arrow-upward';
    import IconUnfoldMore from '~icons/mdi/unfold-more-horizontal';
    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { cn } from '$lib/utils';

    type Props = HTMLAttributes<Element> & {
        column: Column<TData, unknown>;
    };

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
            <DropdownMenu.Trigger asChild let:builder>
                <Button variant="ghost" builders={[builder]} class="-ml-3 h-8 data-[state=open]:bg-accent">
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
            </DropdownMenu.Trigger>
            <DropdownMenu.Content align="start">
                <DropdownMenu.Item on:click={handleAscSort}>Asc</DropdownMenu.Item>
                <DropdownMenu.Item on:click={handleDescSort}>Desc</DropdownMenu.Item>
            </DropdownMenu.Content>
        </DropdownMenu.Root>
    </div>
{:else if children}
    {@render children()}
{/if}
