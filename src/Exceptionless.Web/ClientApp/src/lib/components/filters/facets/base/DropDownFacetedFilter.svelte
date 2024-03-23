<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import { writable } from 'svelte/store';
    import IconCheck from '~icons/mdi/check';

    import { Button } from '$comp/ui/button';
    import * as Command from '$comp/ui/command';
    import * as Popover from '$comp/ui/popover';
    import Separator from '$comp/ui/separator/separator.svelte';
    import Loading from '$comp/Loading.svelte';
    import { cn } from '$lib/utils';
    import * as FacetedFilter from '$comp/faceted-filter';

    type Option = {
        value: string;
        label: string;
    };

    export let loading: boolean = false;
    export let title: string;
    export let value: string;
    export let options: Option[];

    const updatedValue = writable<string>(value);
    const open = writable<boolean>(false);
    open.subscribe(($open) => {
        if ($open) {
            updatedValue.set(value);
        } else if ($updatedValue !== value) {
            value = $updatedValue;
            dispatch('changed', value);
        }
    });

    const dispatch = createEventDispatcher();
    export function onValueSelected(currentValue: string) {
        if ($updatedValue === currentValue) {
            updatedValue.set('');
        } else {
            updatedValue.set(currentValue);
        }
    }

    export function onClearFilter() {
        updatedValue.set('');
    }

    function onRemoveFilter(): void {
        value = '';
        dispatch('remove');
    }

    function displayValue(value: string) {
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

<Popover.Root bind:open={$open}>
    <Popover.Trigger asChild let:builder>
        <Button builders={[builder]} variant="outline" size="sm" class="h-8">
            {title}
            <Separator orientation="vertical" class="mx-2 h-4" />
            {#if loading}
                <FacetedFilter.BadgeLoading />
            {:else if value}
                <FacetedFilter.BadgeValue>{displayValue(value)}</FacetedFilter.BadgeValue>
            {:else}
                <FacetedFilter.BadgeValue>No Value</FacetedFilter.BadgeValue>
            {/if}
        </Button>
    </Popover.Trigger>
    <Popover.Content class="p-0" align="start" side="bottom">
        <Command.Root {filter}>
            <Command.Input placeholder={title} />
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
                                    $updatedValue === option.value ? 'bg-primary text-primary-foreground' : 'opacity-50 [&_svg]:invisible'
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
            showApply={$updatedValue !== value}
            onApply={() => open.set(false)}
            showClear={!!$updatedValue?.trim()}
            onClear={onClearFilter}
            onRemove={onRemoveFilter}
        ></FacetedFilter.Actions>
    </Popover.Content>
</Popover.Root>
