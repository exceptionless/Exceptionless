<script lang="ts">
    import * as FacetedFilter from '$comp/faceted-filter';
    import { Button } from '$comp/ui/button';
    import { Input } from '$comp/ui/input';
    import * as Popover from '$comp/ui/popover';
    import Separator from '$comp/ui/separator/separator.svelte';

    interface Props {
        changed: (value?: string) => void;
        open: boolean;
        remove: () => void;
        title: string;
        value?: string;
    }

    let { changed, open = $bindable(), remove, title = 'Keyword', value }: Props = $props();
    // eslint-disable-next-line svelte/prefer-writable-derived
    let updatedValue = $state(value);

    $effect(() => {
        updatedValue = value;
    });

    function handleKeyDown(event: KeyboardEvent) {
        if (event.key === 'Enter') {
            event.preventDefault();
            onClose();
        } else if (event.key === 'Escape') {
            event.preventDefault();
            onCancel();
        }
    }

    function onClose() {
        if (updatedValue !== value) {
            changed(updatedValue);
        }

        open = false;
    }

    function onCancel() {
        updatedValue = value;
        open = false;
    }

    function onOpenChange(isOpen: boolean) {
        if (!isOpen) {
            onClose();
        }
    }

    export function onClearFilter() {
        updatedValue = undefined;
    }
</script>

<Popover.Root bind:open {onOpenChange}>
    <Popover.Trigger>
        {#snippet child({ props })}
            <Button {...props} class="gap-x-1 px-3" size="lg" variant="outline" aria-describedby={`${title}-help`}>
                {title}
                <Separator class="mx-2" orientation="vertical" />
                {#if value}
                    <FacetedFilter.BadgeValue>{value}</FacetedFilter.BadgeValue>
                {:else}
                    <FacetedFilter.BadgeValue>No Value</FacetedFilter.BadgeValue>
                {/if}
            </Button>
        {/snippet}
    </Popover.Trigger>
    <Popover.Content align="start" class="p-0" side="bottom">
        <div class="flex items-center border-b">
            <Input
                bind:value={updatedValue}
                placeholder={title}
                type="text"
                aria-label={`Filter by ${title}`}
                aria-describedby={`${title}-help`}
                onkeydown={handleKeyDown}
                autofocus={open}
            />
        </div>
        <div id={`${title}-help`} class="sr-only">Press Enter to apply filter, Escape to cancel</div>
        <FacetedFilter.Actions clear={onClearFilter} {remove} showClear={!!updatedValue?.trim()} />
    </Popover.Content>
</Popover.Root>
