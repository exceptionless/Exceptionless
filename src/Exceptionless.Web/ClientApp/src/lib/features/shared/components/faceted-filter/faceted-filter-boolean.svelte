<script lang="ts">
    import * as FacetedFilter from '$comp/faceted-filter';
    import { Button } from '$comp/ui/button';
    import { Input } from '$comp/ui/input';
    import * as Popover from '$comp/ui/popover';
    import Separator from '$comp/ui/separator/separator.svelte';

    interface Props {
        changed: (value?: boolean) => void;
        open: boolean;
        remove: () => void;
        title: string;
        value?: boolean;
    }

    let { changed, open = $bindable(), remove, title, value }: Props = $props();
    // eslint-disable-next-line svelte/prefer-writable-derived
    let updatedValue = $state(value);

    $effect(() => {
        updatedValue = value;
    });

    function onClose() {
        if (updatedValue !== value) {
            changed(updatedValue);
        }

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
        <Button class="gap-x-1 px-3" size="lg" variant="outline">
            {title}
            <Separator class="mx-2" orientation="vertical" />
            {#if value}
                <FacetedFilter.BadgeValue>{value}</FacetedFilter.BadgeValue>
            {:else}
                <FacetedFilter.BadgeValue>No Value</FacetedFilter.BadgeValue>
            {/if}
        </Button>
    </Popover.Trigger>
    <Popover.Content align="start" class="p-0" side="bottom">
        <div class="flex items-center border-b">
            <Input bind:value={updatedValue} placeholder={title} type="boolean" />
        </div>
        <FacetedFilter.Actions clear={onClearFilter} close={onClose} {remove} showClear={updatedValue !== undefined} />
    </Popover.Content>
</Popover.Root>
