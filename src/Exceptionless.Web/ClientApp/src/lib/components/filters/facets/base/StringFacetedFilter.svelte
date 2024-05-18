<script lang="ts">
    import { Button } from '$comp/ui/button';
    import { Input } from '$comp/ui/input';
    import * as Popover from '$comp/ui/popover';
    import * as FacetedFilter from '$comp/faceted-filter';
    import Separator from '$comp/ui/separator/separator.svelte';

    interface Props {
        title: string;
        value?: string;
        open: boolean;
        changed: () => void;
        remove: () => void;
    }

    let { title, value = $bindable(), open = $bindable(), changed, remove }: Props = $props();
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

    export function onClearFilter() {
        updatedValue = undefined;
    }

    function onRemoveFilter(): void {
        value = undefined;
        remove();
    }
</script>

<Popover.Root bind:open>
    <Popover.Trigger asChild let:builder>
        <Button builders={[builder]} variant="outline" size="sm" class="h-8">
            {title}
            <Separator orientation="vertical" class="mx-2 h-4" />
            {#if value?.trim()}
                <FacetedFilter.BadgeValue>{value}</FacetedFilter.BadgeValue>
            {:else}
                <FacetedFilter.BadgeValue>No Value</FacetedFilter.BadgeValue>
            {/if}
        </Button>
    </Popover.Trigger>
    <Popover.Content class="p-0" align="start" side="bottom">
        <div class="flex items-center border-b">
            <Input type="text" placeholder={title} bind:value={updatedValue} />
        </div>
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
