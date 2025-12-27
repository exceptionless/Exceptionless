<script lang="ts">
    import * as FacetedFilter from '$comp/faceted-filter';
    import { Button } from '$comp/ui/button';
    import { Input } from '$comp/ui/input';
    import * as Popover from '$comp/ui/popover';
    import Separator from '$comp/ui/separator/separator.svelte';

    interface Props {
        changed: (value?: number) => void;
        open: boolean;
        remove: () => void;
        title: string;
        value?: number;
    }

    let { changed, open = $bindable(), remove, title, value }: Props = $props();

    // eslint-disable-next-line svelte/prefer-writable-derived
    let updatedValue = $state<number | undefined>();

    $effect.pre(() => {
        updatedValue = value;
    });

    function handleKeyDown(event: KeyboardEvent) {
        if (event.key === 'Enter') {
            event.preventDefault();
            applyAndClose();
        } else if (event.key === 'Escape') {
            event.preventDefault();
            cancelAndClose();
        }
    }

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
                {#if value !== undefined && !isNaN(value)}
                    <FacetedFilter.BadgeValue>{value}</FacetedFilter.BadgeValue>
                {:else}
                    <FacetedFilter.BadgeValue>No Value</FacetedFilter.BadgeValue>
                {/if}
            </Button>
        {/snippet}
    </Popover.Trigger>
    <Popover.Content align="start" class="p-0" side="bottom" trapFocus={false} {onEscapeKeydown} onFocusOutside={applyAndClose}>
        <div class="flex items-center border-b">
            <Input
                bind:value={updatedValue}
                placeholder={title}
                type="number"
                aria-label={`Filter by ${title}`}
                aria-describedby={`${title}-help`}
                onkeydown={handleKeyDown}
                autofocus={open}
            />
        </div>
        <div id={`${title}-help`} class="sr-only">Type a number. Enter applies, Escape cancels.</div>
        <FacetedFilter.Actions clear={onClearFilter} {remove} showClear={updatedValue !== undefined} />
    </Popover.Content>
</Popover.Root>
