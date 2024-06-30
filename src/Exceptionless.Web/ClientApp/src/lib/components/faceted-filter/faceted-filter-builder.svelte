<script lang="ts">
    import { toast } from 'svelte-sonner';
    import IconAddCircleOutline from '~icons/mdi/add-circle-outline';

    import * as Command from '$comp/ui/command';
    import * as Popover from '$comp/ui/popover';
    import { Button } from '$comp/ui/button';
    import type { IFilter } from '$comp/filters/filters';
    import type { FacetedFilter } from '.';

    interface Props {
        facets: FacetedFilter[];
        changed: (filter: IFilter) => void;
        remove: (filter?: IFilter) => void;
    }

    let { facets, changed, remove }: Props = $props();

    let open = $state(false);
    let visible = $state<string[]>([]);

    function onFacetSelected(facet: FacetedFilter) {
        facets.forEach((f) => f.open.set(false));

        if (visible.includes(facet.filter.key)) {
            toast.error(`Only one ${facet.title} filter can be applied at a time.`);
        } else {
            visible = [...visible, facet.filter.key];
        }

        open = false;
        facet.open.set(true);
    }

    function onChanged({ detail }: CustomEvent<IFilter>) {
        changed(detail);
    }

    function onRemove({ detail }: CustomEvent<IFilter>) {
        visible = visible.filter((key) => key !== detail.key);

        if (!detail.isEmpty()) {
            detail.reset();
        }

        remove(detail);
    }

    function onRemoveAll() {
        visible = [];
        facets.forEach((facet) => facet.filter.reset());
        remove();
    }

    function onClose() {
        open = false;
    }

    function isVisible(facet: FacetedFilter): boolean {
        // Add any new facets that have been synced from storage.
        const visibleFacets = [...visible, ...facets.filter((f) => !f.filter.isEmpty() && !visible.includes(f.filter.key)).map((f) => f.filter.key)];
        return visibleFacets.includes(facet.filter.key);
    }
</script>

<Popover.Root bind:open>
    <Popover.Trigger asChild let:builder>
        <Button builders={[builder]} variant="outline" size="sm" class="h-8">
            <IconAddCircleOutline class="mr-2 h-4 w-4" /> Filter
        </Button>
    </Popover.Trigger>
    <Popover.Content class="w-[200px] p-0" align="start" side="bottom">
        <Command.Root>
            <Command.Input placeholder={'Search...'} />
            <Command.List>
                <Command.Empty>No results found.</Command.Empty>
                <Command.Group>
                    {#each facets as facet (facet.filter.key)}
                        <Command.Item value={facet.filter.key} onSelect={() => onFacetSelected(facet)}>{facet.title}</Command.Item>
                    {/each}
                </Command.Group>
            </Command.List>
        </Command.Root>
        <Command.Root>
            <Command.List>
                <Command.Separator />
                {#if visible.length > 0}
                    <Command.Item class="justify-center text-center" onSelect={onRemoveAll}>Clear filters</Command.Item>
                {/if}
                <Command.Item class="justify-center text-center" onSelect={onClose}>Close</Command.Item>
            </Command.List>
        </Command.Root>
    </Popover.Content>
</Popover.Root>

{#each facets as facet (facet.filter.key)}
    {#if isVisible(facet)}
        <svelte:component this={facet.component} filter={facet.filter} title={facet.title} open={facet.open} on:changed={onChanged} on:remove={onRemove} />
    {/if}
{/each}
