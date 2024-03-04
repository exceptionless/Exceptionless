<script lang="ts">
    import IconAddCircleOutline from '~icons/mdi/add-circle-outline';
    import IconCheck from '~icons/mdi/check';

    import * as Command from '$comp/ui/command';
    import * as Popover from '$comp/ui/popover';
    import { Button } from '$comp/ui/button';

    import Separator from '$comp/ui/separator/separator.svelte';
    import Badge from '$comp/ui/badge/badge.svelte';
    import { cn } from '$lib/utils';
    import { createEventDispatcher } from 'svelte';

    type Option = {
        value: string;
        label: string;
    };

    export let title: string;
    export let values: string[] = [];
    export let options: Option[] = [];

    let open = false;

    const dispatch = createEventDispatcher();
    export function onValueSelected(currentValue: string) {
        values = values.includes(currentValue) ? values.filter((v) => v !== currentValue) : [...values, currentValue];
        dispatch('changed', values);
    }

    export function onClearFilter() {
        values = [];
        dispatch('changed', values);
    }

    function onRemoveFilter(): void {
        dispatch('remove');
    }
</script>

<Popover.Root bind:open>
    <Popover.Trigger asChild let:builder>
        <Button builders={[builder]} variant="outline" size="sm" class="h-8 border-dashed">
            <IconAddCircleOutline class="mr-2 h-4 w-4" />
            {title}

            {#if values.length > 0}
                <Separator orientation="vertical" class="mx-2 h-4" />
                <Badge variant="secondary" class="rounded-sm px-1 font-normal lg:hidden">
                    {values.length}
                </Badge>
                <div class="hidden space-x-1 lg:flex">
                    {#if values.length > 2}
                        <Badge variant="secondary" class="rounded-sm px-1 font-normal">
                            {values.length} Selected
                        </Badge>
                    {:else}
                        {#each options as option (option.value)}
                            {#if values.includes(option.value)}
                                <Badge variant="secondary" class="rounded-sm px-1 font-normal">
                                    {option.label}
                                </Badge>
                            {/if}
                        {/each}
                    {/if}
                </div>
            {/if}
        </Button>
    </Popover.Trigger>
    <Popover.Content class="w-[200px] p-0" align="start" side="bottom">
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
                                    values.includes(option.value) ? 'bg-primary text-primary-foreground' : 'opacity-50 [&_svg]:invisible'
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
                {#if values.length > 0}
                    <Command.Item class="justify-center text-center" onSelect={onClearFilter}>Clear filter</Command.Item>
                {/if}
                <Command.Item class="justify-center text-center" onSelect={onRemoveFilter}>Remove filter</Command.Item>
            </Command.List>
        </Command.Root>
    </Popover.Content>
</Popover.Root>
