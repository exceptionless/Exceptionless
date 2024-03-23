<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import { derived, writable } from 'svelte/store';
    import IconCheck from '~icons/mdi/check';

    import { Button } from '$comp/ui/button';
    import * as Command from '$comp/ui/command';
    import * as Popover from '$comp/ui/popover';
    import Separator from '$comp/ui/separator/separator.svelte';
    import Loading from '$comp/Loading.svelte';
    import * as FacetedFilter from '$comp/faceted-filter';

    import { cn } from '$lib/utils';

    type Option = {
        value: string;
        label: string;
    };

    export let loading: boolean = false;
    export let title: string;
    export let values: string[];
    export let options: Option[];

    const updatedValues = writable<string[]>(values);
    const hasChanged = derived(updatedValues, ($updatedValues) => {
        return $updatedValues.length !== values.length || $updatedValues.some((value) => !values.includes(value));
    });

    const displayValues = derived(updatedValues, ($updatedValues) => {
        const labelsInOptions = options.filter((o) => $updatedValues.includes(o.value)).map((o) => o.label);

        const valuesNotInOptions = $updatedValues.filter((value) => !options.some((o) => o.value === value));

        return [...labelsInOptions, ...valuesNotInOptions];
    });

    const open = writable<boolean>(false);
    open.subscribe(($open) => {
        if ($open) {
            updatedValues.set(values);
        } else if ($hasChanged) {
            values = $updatedValues;
            dispatch('changed', values);
        }
    });

    const dispatch = createEventDispatcher();
    export function onValueSelected(currentValue: string) {
        updatedValues.update(($updatedValues) =>
            $updatedValues.includes(currentValue) ? $updatedValues.filter((v) => v !== currentValue) : [...$updatedValues, currentValue]
        );
    }

    export function onClearFilter() {
        updatedValues.set([]);
    }

    function onRemoveFilter(): void {
        values = [];
        dispatch('remove');
    }

    function filter(value: string, search: string) {
		if (value.includes(search)) {
            return 1;
        }

        var option = options.find((option) => option.value === value);
        if (option?.label.toLowerCase().includes(search)) {
            return 1;
        }

        return 0;
	}
</script>

<Popover.Root bind:open={$open}>
    <Popover.Trigger asChild let:builder>
        <Button builders={[builder]} variant="outline" size="sm" class="h-8">
            {title}
            <Separator orientation="vertical" class="mx-2 h-4" />
            {#if loading}
                <FacetedFilter.BadgeLoading />
            {:else if $displayValues.length > 0}
                <FacetedFilter.BadgeValues values={$displayValues} let:value>
                    {value}
                </FacetedFilter.BadgeValues>
            {:else}
                <FacetedFilter.BadgeValue>No Value</FacetedFilter.BadgeValue>
            {/if}
        </Button>
    </Popover.Trigger>
    <Popover.Content class="p-0" align="start" side="bottom">
        <Command.Root {filter}>
            {#if options.length > 10}
                <Command.Input placeholder={title} />
            {/if}
            <Command.List>
                {#if loading}
                    <Command.Loading><div class="flex p-2"><Loading class="mr-2 h-4 w-4" /> Loading...</div></Command.Loading>
                {/if}
                <Command.Empty>No results found.</Command.Empty>
                <Command.Group>
                    {#each options as option (option.value)}
                        <Command.Item value={option.value} onSelect={onValueSelected}>
                            <div
                                class={cn(
                                    'mr-2 flex h-4 w-4 items-center justify-center rounded-sm border border-primary',
                                    $updatedValues.includes(option.value) ? 'bg-primary text-primary-foreground' : 'opacity-50 [&_svg]:invisible'
                                )}
                            >
                                <IconCheck className={cn('h-4 w-4')} />
                            </div>
                            <span>
                                {option.label}
                            </span>
                        </Command.Item>
                    {/each}
                </Command.Group>
            </Command.List>
        </Command.Root>
        <FacetedFilter.Actions
            showApply={$hasChanged}
            on:apply={() => open.set(false)}
            showClear={$updatedValues.length > 0}
            on:clear={onClearFilter}
            on:remove={onRemoveFilter}
        ></FacetedFilter.Actions>
    </Popover.Content>
</Popover.Root>
