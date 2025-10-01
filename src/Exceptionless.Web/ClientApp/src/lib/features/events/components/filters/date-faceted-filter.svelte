<script lang="ts">
    import type { DateRangePicker as DateRangePickerType } from '$comp/date-range-picker';
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

    let dateRangePickerRef: DateRangePickerType | undefined = $state();
    let shouldApply = $state(true);

    function handleSelect(value: string) {
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

    function onOpenChange(isOpen: boolean) {
        if (!isOpen && shouldApply) {
            dateRangePickerRef?.apply();
        }

        shouldApply = true;
    }

    function handleKeyDown(event: KeyboardEvent) {
        if (event.key === 'Escape') {
            shouldApply = false;
        }
    }

    const showClear = $derived.by(() => filter.value !== undefined);
</script>

<Popover.Root bind:open {onOpenChange}>
    <Popover.Trigger>
        <Button class="gap-x-1 px-3" size="lg" variant="outline">
            {title}
            <Separator class="mx-2" orientation="vertical" />
            <FacetedFilter.BadgeValue>
                <DateMath value={filter.value} />
            </FacetedFilter.BadgeValue>
        </Button>
    </Popover.Trigger>
    <Popover.Content align="start" class="w-auto p-0" side="bottom" onkeydown={handleKeyDown}>
        <div class="flex flex-col">
            <DateRangePicker bind:this={dateRangePickerRef} {quickRanges} value={filter.value} onselect={handleSelect} />
            <FacetedFilter.Actions clear={handleClear} remove={handleRemove} {showClear} />
        </div>
    </Popover.Content>
</Popover.Root>
