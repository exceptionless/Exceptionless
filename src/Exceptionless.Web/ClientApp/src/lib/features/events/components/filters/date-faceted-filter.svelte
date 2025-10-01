<script lang="ts">
    import type { FacetedFilterProps } from '$comp/faceted-filter';

    import { DateRangePicker } from '$comp/date-range-picker';
    import * as FacetedFilter from '$comp/faceted-filter';
    import DateMath from '$comp/formatters/date-math.svelte';
    import { Button } from '$comp/ui/button';
    import * as Popover from '$comp/ui/popover';
    import Separator from '$comp/ui/separator/separator.svelte';
    import { quickRanges } from '$features/shared/components/date-range-picker/quick-ranges';

    import { DateFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, open = $bindable(false), title = 'Date Range' }: FacetedFilterProps<DateFilter> = $props();

    function handleCustomApply(value: string) {
        filter.value = value;
        filterChanged(filter);
        open = false;
    }

    function handleClear() {
        filter.value = undefined;
        filterChanged(filter);
        open = false;
    }

    function handleRemove() {
        filterRemoved(filter);
        open = false;
    }

    const showClear = $derived.by(() => filter.value !== undefined);
</script>

<Popover.Root bind:open>
    <Popover.Trigger>
        <Button class="gap-x-1 px-3" size="lg" variant="outline">
            {title}
            <Separator class="mx-2" orientation="vertical" />
            <FacetedFilter.BadgeValue>
                <DateMath value={filter.value} />
            </FacetedFilter.BadgeValue>
        </Button>
    </Popover.Trigger>
    <Popover.Content align="start" class="w-auto p-0" side="bottom">
        <div class="flex flex-col">
            <DateRangePicker {quickRanges} value={filter.value} onselect={handleCustomApply} />
            <FacetedFilter.Actions clear={handleClear} remove={handleRemove} {showClear} />
        </div>
    </Popover.Content>
</Popover.Root>
