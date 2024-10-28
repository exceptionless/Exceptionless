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

    let { changed, open = $bindable(), remove, title, value }: Props = $props();
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

    export function onClearFilter() {
        updatedValue = undefined;
    }
</script>

<Popover.Root bind:open>
    <Popover.Trigger asChild >
        {#snippet children({ builder })}
                <Button builders={[builder]} class="h-8" size="sm" variant="outline">
                {title}
                <Separator class="mx-2 h-4" orientation="vertical" />
                {#if value?.trim()}
                    <FacetedFilter.BadgeValue>{value}</FacetedFilter.BadgeValue>
                {:else}
                    <FacetedFilter.BadgeValue>No Value</FacetedFilter.BadgeValue>
                {/if}
            </Button>
                    {/snippet}
        </Popover.Trigger>
    <Popover.Content align="start" class="p-0" side="bottom">
        <div class="flex items-center border-b">
            <Input bind:value={updatedValue} placeholder={title} type="text" />
        </div>
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
