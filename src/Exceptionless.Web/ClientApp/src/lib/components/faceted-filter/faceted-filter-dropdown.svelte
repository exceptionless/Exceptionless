<script lang="ts">
    import IconAddCircleOutline from '~icons/mdi/add-circle-outline';
    import IconCheck from '~icons/mdi/check';

    import * as Command from '$comp/ui/command';
    import * as Popover from '$comp/ui/popover';
    import { Button } from '$comp/ui/button';

    import Separator from '$comp/ui/separator/separator.svelte';
    import Badge from '$comp/ui/badge/badge.svelte';
    import { createEventDispatcher } from 'svelte';
    import { writable } from 'svelte/store';
    import { cn } from '$lib/utils';
    import { Loading } from '$comp/ui/command';

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
        updatedValue.set(currentValue);
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
</script>

<Popover.Root bind:open={$open}>
    <Popover.Trigger asChild let:builder>
        <Button builders={[builder]} variant="outline" size="sm" class="h-8 border-dashed">
            <IconAddCircleOutline class="mr-2 h-4 w-4" />
            {title}

            {#if loading}
                <Separator orientation="vertical" class="mx-2 h-4" />
                <Badge variant="secondary" class="rounded-sm px-1 font-normal">
                    <Loading class="mr-2 h-4 w-4" /> Loading
                </Badge>
            {:else if value}
                <Separator orientation="vertical" class="mx-2 h-4" />
                <Badge variant="secondary" class="rounded-sm px-1 font-normal lg:hidden">
                    <span class="max-w-24 truncate">{displayValue(value)}</span>
                </Badge>
                <div class="hidden space-x-1 lg:flex">
                    <Badge variant="secondary" class="rounded-sm px-1 font-normal">
                        <span class="max-w-60 truncate">{displayValue(value)}</span>
                    </Badge>
                </div>
            {/if}
        </Button>
    </Popover.Trigger>
    <Popover.Content class="p-0 lg:w-[350px] xl:w-[550px]" align="start" side="bottom">
        <Command.Root>
            <Command.Input placeholder={title} />
            <Command.List>
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
                <Command.Separator />
                {#if $updatedValue !== value}
                    <Command.Item class="justify-center text-center font-bold text-primary" onSelect={() => open.set(false)}>Apply filter</Command.Item>
                    <Command.Separator />   
                {/if}
                {#if $updatedValue?.trim()}
                    <Command.Item class="justify-center text-center" onSelect={onClearFilter}>Clear filter</Command.Item>
                {/if}
                <Command.Item class="justify-center text-center" onSelect={onRemoveFilter}>Remove filter</Command.Item>
            </Command.List>
        </Command.Root>
    </Popover.Content>
</Popover.Root>
