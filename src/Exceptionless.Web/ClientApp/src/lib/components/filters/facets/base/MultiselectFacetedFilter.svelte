<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import { derived, writable, type Writable } from 'svelte/store';
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
    export let noOptionsText: string = 'No results found.';
    export let open: Writable<boolean>;

    const updatedValues = writable<string[]>(values);
    const hasChanged = derived(updatedValues, ($updatedValues) => {
        return $updatedValues.length !== values.length || $updatedValues.some((value) => !values.includes(value));
    });

    // bind:open doesn't trigger subscriptions when the variable changes. It only updates the value of the variable.
    open.subscribe(() => updatedValues.set(values));
    $: updatedValues.set(values);

    const dispatch = createEventDispatcher();
    function onApplyFilter() {
        if ($hasChanged) {
            values = $updatedValues;
            dispatch('changed', values);
        }

        open.set(false);
    }

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

    function getDisplayValues(values: string[]): string[] {
        const labelsInOptions = options.filter((o) => values.includes(o.value)).map((o) => o.label);
        const valuesNotInOptions = values.filter((value) => !options.some((o) => o.value === value));
        return [...labelsInOptions, ...valuesNotInOptions];
    }
</script>

<Popover.Root bind:open={$open}>
    <Popover.Trigger asChild let:builder>
        <Button builders={[builder]} variant="outline" size="sm" class="h-8">
            {title}
            <Separator orientation="vertical" class="mx-2 h-4" />
            {#if loading}
                <FacetedFilter.BadgeLoading />
            {:else if values.length > 0}
                <FacetedFilter.BadgeValues values={getDisplayValues(values)} let:value>
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
                <Command.Empty>{noOptionsText}</Command.Empty>
                {#if options.length > 0}
                    <Command.Group>
                        {#each options as option (option.value)}
                            <Command.Item id={option.value} value={option.value} onSelect={() => onValueSelected(option.value)}>
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
                {/if}
            </Command.List>
        </Command.Root>
        <FacetedFilter.Actions
            showApply={$hasChanged}
            on:apply={onApplyFilter}
            showClear={$updatedValues.length > 0}
            on:clear={onClearFilter}
            on:remove={onRemoveFilter}
            on:close={() => open.set(false)}
        ></FacetedFilter.Actions>
    </Popover.Content>
</Popover.Root>
