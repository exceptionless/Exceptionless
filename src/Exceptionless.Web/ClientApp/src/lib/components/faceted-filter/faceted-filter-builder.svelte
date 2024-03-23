<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import type { Readable } from 'svelte/store';

    import IconAddCircleOutline from '~icons/mdi/add-circle-outline';
    import IconCheck from '~icons/mdi/check';

    import * as Command from '$comp/ui/command';
    import * as Popover from '$comp/ui/popover';
    import { Button } from '$comp/ui/button';
    import type { IFilter } from '$comp/filters/filters';
    import { cn } from '$lib/utils';
    import type { FacetedFilter } from '.';

    const dispatch = createEventDispatcher();

    export let facets: Readable<FacetedFilter[]>;

    let open = false;
    let visible: string[] = [];
    facets.subscribe(($facets) => {
        // Add any new facets that have been synced from storage.
        visible = [...visible, ...$facets.filter((f) => !f.filter.isEmpty() && !visible.includes(f.filter.type)).map((f) => f.filter.type)];
    });

    function onFacetSelected(facet: FacetedFilter) {
        if (visible.includes(facet.filter.type)) {
            visible = visible.filter((type) => type !== facet.filter.type);

            if (!facet.filter.isEmpty()) {
                facet.filter.reset();
                onFilterChanged(facet.filter);
            }
        } else {
            visible = [...visible, facet.filter.type];
        }
    }

    function onChanged({ detail }: CustomEvent<IFilter>) {
        onFilterChanged(detail);
    }

    function onFilterChanged(filter?: IFilter) {
        dispatch('changed', filter);
    }

    function onRemove({ detail }: CustomEvent<IFilter>) {
        visible = visible.filter((type) => type !== detail.type);

        if (!detail.isEmpty()) {
            detail.reset();
            onFilterChanged(detail);
        }
    }

    function onRemoveAll() {
        visible = [];
        if ($facets.every((f) => f.filter.isEmpty())) {
            return;
        }

        $facets.forEach((facet) => facet.filter.reset());
        onFilterChanged();
    }
</script>

<Popover.Root bind:open>
    <Popover.Trigger asChild let:builder>
        <Button builders={[builder]} variant="outline" size="sm" class="h-8 border-dashed">
            <IconAddCircleOutline class="mr-2 h-4 w-4" /> Filter
        </Button>
    </Popover.Trigger>
    <Popover.Content class="w-[200px] p-0" align="start" side="bottom">
        <Command.Root>
            <Command.Input placeholder={'Search...'} />
            <Command.List>
                <Command.Empty>No results found.</Command.Empty>
                <Command.Group>
                    {#each $facets as facet (facet.filter.type)}
                        <Command.Item value={facet.filter.type} onSelect={() => onFacetSelected(facet)}>
                            <div
                                class={cn(
                                    'mr-2 flex h-4 w-4 items-center justify-center rounded-sm border border-primary',
                                    visible.includes(facet.filter.type) ? 'bg-primary text-primary-foreground' : 'opacity-50 [&_svg]:invisible'
                                )}
                            >
                                <IconCheck className={cn('h-4 w-4')} />
                            </div>
                            <span>
                                {facet.title}
                            </span>
                        </Command.Item>
                    {/each}
                </Command.Group>
                {#if visible.length > 0}
                    <Command.Separator />
                    <Command.Item class="justify-center text-center" onSelect={onRemoveAll}>Clear filters</Command.Item>
                {/if}
            </Command.List>
        </Command.Root>
    </Popover.Content>
</Popover.Root>

{#each $facets as facet (facet.filter.type)}
    {#if visible.includes(facet.filter.type)}
        <svelte:component this={facet.component} filter={facet.filter} title={facet.title} on:changed={onChanged} on:remove={onRemove} />
    {/if}
{/each}
