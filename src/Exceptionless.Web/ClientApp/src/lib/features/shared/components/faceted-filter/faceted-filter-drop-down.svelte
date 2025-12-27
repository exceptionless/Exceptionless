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
        changed: (value?: string) => void;
        loading?: boolean;
        noOptionsText?: string;
        open: boolean;
        options: Option[];
        remove: () => void;
        title: string;
        value?: string;
    }

    let { changed, loading = false, noOptionsText = 'No results found.', open = $bindable(), options, remove, title, value }: Props = $props();

    // eslint-disable-next-line svelte/prefer-writable-derived
    let updatedValue = $state<string | undefined>();

    $effect.pre(() => {
        updatedValue = value;
    });

    function applyAndClose() {
        if (updatedValue !== value) {
            changed(updatedValue);
        }

        open = false;
    }

    function cancelAndClose() {
        updatedValue = value;
        open = false;
    }

    function onOpenChange(isOpen: boolean) {
        if (!isOpen) {
            applyAndClose();
        }
    }

    function onEscapeKeydown(e: KeyboardEvent) {
        e.preventDefault();
        cancelAndClose();
    }

    export function onValueSelected(currentValue: string) {
        if (updatedValue === currentValue) {
            updatedValue = undefined;
        } else {
            updatedValue = currentValue;
        }
    }

    export function onClearFilter() {
        updatedValue = undefined;
    }

    function displayValue(value: string | undefined) {
        return options.find((option) => option.value === value)?.label ?? value;
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
                {:else if value !== undefined}
                    <FacetedFilter.BadgeValue>{displayValue(value)}</FacetedFilter.BadgeValue>
                {:else}
                    <FacetedFilter.BadgeValue>No Value</FacetedFilter.BadgeValue>
                {/if}
            </Button>
        {/snippet}
    </Popover.Trigger>
    <Popover.Content align="start" class="p-0" side="bottom" trapFocus={false} {onEscapeKeydown} onFocusOutside={applyAndClose}>
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
                                        updatedValue === option.value ? 'bg-primary text-primary-foreground' : 'opacity-50 [&_svg]:invisible'
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
        <FacetedFilter.Actions clear={onClearFilter} {remove} showClear={!!updatedValue?.trim()} />
    </Popover.Content>
</Popover.Root>
