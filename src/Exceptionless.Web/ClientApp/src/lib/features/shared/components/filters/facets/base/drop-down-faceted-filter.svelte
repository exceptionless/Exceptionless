<script lang="ts">
    import * as FacetedFilter from '$comp/faceted-filter';
    import Loading from '$comp/loading.svelte';
    import { Button } from '$comp/ui/button';
    import * as Command from '$comp/ui/command';
    import * as Popover from '$comp/ui/popover';
    import Separator from '$comp/ui/separator/separator.svelte';
    import { cn } from '$lib/utils';
    import Check from 'lucide-svelte/icons/check';

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
    let updatedValue = $state(value);

    $effect(() => {
        updatedValue = value;
    });

    function onApplyFilter() {
        if (updatedValue !== value) {
            changed(updatedValue);
        }

        open = false;
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

        var option = options.find((option) => option.value === value);
        if (option?.label.toLowerCase().includes(search)) {
            return 1;
        }

        return 0;
    }
</script>

<Popover.Root bind:open>
    <Popover.Trigger>
        {#snippet children()}
            <Button class="h-8" size="sm" variant="outline">
                {title}
                <Separator class="mx-2 h-4" orientation="vertical" />
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
    <Popover.Content align="start" class="p-0" side="bottom">
        <Command.Root {filter}>
            {#if options.length > 10}
                <Command.Input placeholder={title} />
            {/if}
            <Command.List>
                <Command.Empty>{noOptionsText}</Command.Empty>
                {#if loading}
                    <Command.Loading><div class="flex p-2"><Loading class="mr-2 size-4" /> Loading...</div></Command.Loading>
                {/if}
                {#if options.length > 0}
                    <Command.Group>
                        {#each options as option (option.value)}
                            <Command.Item id={option.value} onSelect={() => onValueSelected(option.value)} value={option.value}>
                                <div
                                    class={cn(
                                        'mr-2 flex size-4 items-center justify-center rounded-sm border border-primary',
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
                    </Command.Group>{/if}
            </Command.List>
        </Command.Root>
        <FacetedFilter.Actions
            apply={onApplyFilter}
            clear={onClearFilter}
            close={() => (open = false)}
            {remove}
            showApply={updatedValue !== value}
            showClear={!!updatedValue?.trim()}
        ></FacetedFilter.Actions>
    </Popover.Content>
</Popover.Root>
