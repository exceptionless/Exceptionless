<script lang="ts">
    import IconAddCircleOutline from '~icons/mdi/add-circle-outline';
    import IconCheck from '~icons/mdi/check';
    import IconClose from '~icons/mdi/close';

    import * as Command from '$comp/ui/command';
    import * as Popover from '$comp/ui/popover';
    import { Button } from '$comp/ui/button';
    import type { IFilter } from '$comp/filters/filters';
    import { cn } from '$lib/utils';
    import { createEventDispatcher, type ComponentType } from 'svelte';

    const dispatch = createEventDispatcher();
    let visible: string[] = [];

    type FacetedFilter = { title: string; type: string; component: ComponentType; filter?: IFilter };
    export let facets: FacetedFilter[] = [];

    let open = false;

    function onFacetSelected(type: string) {
        if (visible.includes(type)) {
            visible = visible.filter((item) => item !== type);
        } else {
            visible = [...visible, type];
        }
        console.log('selected', visible);
    }

    function onChanged({ detail }: CustomEvent<IFilter>) {
        console.log('changed', detail.toFilter());
        dispatch('changed', detail);
    }

    function onRemove({ detail }: CustomEvent<IFilter>) {
        console.log('remove', detail.type);
        visible = visible.filter((item) => item !== detail.type);
    }

    function onRemoveAll() {
        console.log('remove all');
        visible = [];
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
                    {#each facets as facet (facet.type)}
                        <Command.Item value={facet.type} onSelect={onFacetSelected}>
                            <div
                                class={cn(
                                    'mr-2 flex h-4 w-4 items-center justify-center rounded-sm border border-primary',
                                    visible.includes(facet.type) ? 'bg-primary text-primary-foreground' : 'opacity-50 [&_svg]:invisible'
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

{#each facets as facet (facet.type)}
    {#if visible.includes(facet.type)}
        <svelte:component this={facet.component} on:changed={onChanged} on:remove={onRemove} />
    {/if}
{/each}

{#if visible.length > 0}
    <Button on:click={onRemoveAll} variant="ghost" class="h-8 px-2 lg:px-3">
        Reset
        <IconClose class="ml-2 h-4 w-4" />
    </Button>
{/if}
