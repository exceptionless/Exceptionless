<script lang="ts">
    import * as FacetedFilter from '$comp/faceted-filter';
    import { Button } from '$comp/ui/button';
    import { Input } from '$comp/ui/input';
    import * as Popover from '$comp/ui/popover';
    import Separator from '$comp/ui/separator/separator.svelte';
    import { onDestroy } from 'svelte';

    interface Props {
        changed: (value?: number) => void;
        hidden?: boolean;
        open: boolean;
        remove: () => void;
        title: string;
        toggleHidden?: () => void;
        value?: number;
    }

    let { changed, hidden = false, open = $bindable(), remove, title, toggleHidden, value }: Props = $props();

    const DEBOUNCE_MS = 500;

    // eslint-disable-next-line svelte/prefer-writable-derived
    let updatedValue = $state<number | undefined>();
    let debounceTimer: ReturnType<typeof setTimeout> | undefined;

    onDestroy(() => clearTimeout(debounceTimer));

    $effect.pre(() => {
        updatedValue = value;
    });

    function scheduleApply() {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => {
            if (updatedValue !== value) {
                changed(updatedValue);
            }
        }, DEBOUNCE_MS);
    }

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
        clearTimeout(debounceTimer);
        if (updatedValue !== value) {
            changed(updatedValue);
        }

        open = false;
    }

    function cancelAndClose() {
        clearTimeout(debounceTimer);
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
    <Popover.Content align="start" class="p-0" side="bottom" trapFocus={false} {onEscapeKeydown} onFocusOutside={(e) => e.preventDefault()}>
        <div class="p-2">
            <Input
                bind:value={updatedValue}
                placeholder={title}
                type="number"
                aria-label={`Filter by ${title}`}
                aria-describedby={`${title}-help`}
                onkeydown={handleKeyDown}
                oninput={scheduleApply}
                autofocus={open}
            />
        </div>
        <div id={`${title}-help`} class="sr-only">Type a number. Enter applies, Escape cancels.</div>
        <FacetedFilter.Actions clear={onClearFilter} {hidden} {remove} showClear={updatedValue !== undefined} {toggleHidden} />
    </Popover.Content>
</Popover.Root>
