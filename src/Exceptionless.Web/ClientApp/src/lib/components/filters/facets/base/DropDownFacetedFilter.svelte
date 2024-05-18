<script lang="ts">
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

    interface Props {
        title: string;
        value?: string;
        options: Option[];
        noOptionsText?: string;
        loading?: boolean;
        open: boolean;
        changed: () => void;
        remove: () => void;
    }

    let { title, value = $bindable(), options, noOptionsText = 'No results found.', loading = false, open = $bindable(), changed, remove }: Props = $props();
    let updatedValue = $state(value);

    $effect(() => {
        updatedValue = value;
    });

    function onApplyFilter() {
        if (updatedValue !== value) {
            value = updatedValue;
            changed();
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

    function onRemoveFilter(): void {
        value = undefined;
        remove();
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
    <Popover.Trigger asChild let:builder>
        <Button builders={[builder]} variant="outline" size="sm" class="h-8">
            {title}
            <Separator orientation="vertical" class="mx-2 h-4" />
            {#if loading}
                <FacetedFilter.BadgeLoading />
            {:else if value !== undefined}
                <FacetedFilter.BadgeValue>{displayValue(value)}</FacetedFilter.BadgeValue>
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
                                        updatedValue === option.value ? 'bg-primary text-primary-foreground' : 'opacity-50 [&_svg]:invisible'
                                    )}
                                >
                                    <IconCheck className={cn('h-4 w-4')} />
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
            showApply={updatedValue !== value}
            on:apply={onApplyFilter}
            showClear={!!updatedValue?.trim()}
            on:clear={onClearFilter}
            on:remove={onRemoveFilter}
            on:close={() => (open = false)}
        ></FacetedFilter.Actions>
    </Popover.Content>
</Popover.Root>
