<script lang="ts">
    import * as FacetedFilter from '$comp/faceted-filter';
    import { Button } from '$comp/ui/button';
    import * as Command from '$comp/ui/command';
    import * as Popover from '$comp/ui/popover';
    import Separator from '$comp/ui/separator/separator.svelte';
    import { Spinner } from '$comp/ui/spinner';
    import { cn } from '$lib/utils';
    import Check from '@lucide/svelte/icons/check';

    type Option = {
        label: string;
        value: string;
    };

    interface Props {
        changed: (values: string[]) => void;
        hidden?: boolean;
        loading?: boolean;
        noOptionsText?: string;
        open: boolean;
        options: Option[];
        remove: () => void;
        title: string;
        toggleHidden?: () => void;
        values: string[];
    }

    let {
        changed,
        hidden = false,
        loading = false,
        noOptionsText = 'No results found.',
        open = $bindable(),
        options,
        remove,
        title,
        toggleHidden,
        values
    }: Props = $props();

    // eslint-disable-next-line svelte/prefer-writable-derived
    let updatedValues = $state<string[]>([]);
    let displayValues = $derived.by(() => {
        const labelsInOptions = options.filter((o) => values.includes(o.value)).map((o) => o.label);
        const valuesNotInOptions = values.filter((value) => !options.some((o) => o.value === value));
        return [...labelsInOptions, ...valuesNotInOptions];
    });

    $effect.pre(() => {
        updatedValues = values;
    });

    function cancelAndClose() {
        updatedValues = values;
        changed(updatedValues);
        open = false;
    }

    function onOpenChange(isOpen: boolean) {
        if (!isOpen) {
            open = false;
        }
    }

    function onEscapeKeydown(e: KeyboardEvent) {
        e.preventDefault();
        cancelAndClose();
    }

    export function onValueSelected(currentValue: string) {
        updatedValues = updatedValues.includes(currentValue) ? updatedValues.filter((v) => v !== currentValue) : [...updatedValues, currentValue];
        changed(updatedValues);
    }

    export function onClearFilter() {
        updatedValues = [];
        changed(updatedValues);
    }

    function filter(value: string, search: string) {
        if (value.includes(search)) {
            return 1;
        }

        const option = options.find((option) => option.value === value);
        if (option?.label.toLowerCase().includes(search)) {
            return 1;
        }

        return 0;
    }
</script>

<Popover.Root bind:open {onOpenChange}>
    <Popover.Trigger>
        {#snippet child({ props })}
            <Button {...props} class="gap-x-1 px-3" size="lg" variant="outline" aria-describedby={`${title}-help`}>
                {title}
                <Separator class="mx-2" orientation="vertical" />
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
        {/snippet}
    </Popover.Trigger>
    <Popover.Content align="start" class="p-0" side="bottom" trapFocus={false} {onEscapeKeydown} onFocusOutside={(e) => e.preventDefault()}>
        <Command.Root {filter}>
            <Command.Input placeholder={title} autofocus={open} aria-describedby={`${title}-help`} />
            <Command.List>
                <Command.Empty>{noOptionsText}</Command.Empty>
                {#if loading}
                    <Command.Loading><div class="flex p-2"><Spinner /> Loading...</div></Command.Loading>
                {/if}
                {#if options.length > 0}
                    <Command.Group>
                        {#each options as option (option.value)}
                            <Command.Item id={option.value} onSelect={() => onValueSelected(option.value)} value={option.value}>
                                <div
                                    class={cn(
                                        'border-primary mr-2 flex size-4 items-center justify-center rounded-sm border',
                                        updatedValues.includes(option.value) ? 'bg-primary text-primary-foreground' : 'opacity-50 [&_svg]:invisible'
                                    )}
                                >
                                    <Check className={cn('size-4')} />
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
        <div id={`${title}-help`} class="sr-only">Arrow keys navigate. Space or Enter toggles selection. Escape cancels without saving.</div>
        <FacetedFilter.Actions clear={onClearFilter} {hidden} {remove} showClear={updatedValues.length > 0} {toggleHidden} />
    </Popover.Content>
</Popover.Root>
