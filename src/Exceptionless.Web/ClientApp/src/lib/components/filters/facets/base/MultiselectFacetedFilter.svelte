<script lang="ts">
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

    interface Props {
        title: string;
        values: string[];
        options: Option[];
        noOptionsText?: string;
        loading?: boolean;
        open: boolean;
        changed: (values: string[]) => void;
        remove: () => void;
    }

    let { title, values, options, noOptionsText = 'No results found.', loading = false, open = $bindable(), changed, remove }: Props = $props();
    let updatedValues = $state(values);
    let displayValues = $derived.by(() => {
        const labelsInOptions = options.filter((o) => values.includes(o.value)).map((o) => o.label);
        const valuesNotInOptions = values.filter((value) => !options.some((o) => o.value === value));
        return [...labelsInOptions, ...valuesNotInOptions];
    });

    $effect(() => {
        updatedValues = values;
    });

    const hasChanged = $derived(updatedValues.length !== values.length || updatedValues.some((value) => !values.includes(value)));

    function onApplyFilter() {
        if (hasChanged) {
            changed(updatedValues);
        }

        open = false;
    }

    export function onValueSelected(currentValue: string) {
        updatedValues = updatedValues.includes(currentValue) ? updatedValues.filter((v) => v !== currentValue) : [...updatedValues, currentValue];
    }

    export function onClearFilter() {
        updatedValues = [];
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

<Popover.Root bind:open>
    <Popover.Trigger asChild let:builder>
        <Button builders={[builder]} variant="outline" size="sm" class="h-8">
            {title}
            <Separator orientation="vertical" class="mx-2 h-4" />
            {#if loading}
                <FacetedFilter.BadgeLoading />
            {:else if values.length > 0}
                <FacetedFilter.BadgeValues values={displayValues}>
                    {#snippet displayValue(value)}
                        {value}
                    {/snippet}
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
                                        updatedValues.includes(option.value) ? 'bg-primary text-primary-foreground' : 'opacity-50 [&_svg]:invisible'
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
            showApply={hasChanged}
            apply={onApplyFilter}
            showClear={updatedValues.length > 0}
            clear={onClearFilter}
            {remove}
            close={() => (open = false)}
        ></FacetedFilter.Actions>
    </Popover.Content>
</Popover.Root>
