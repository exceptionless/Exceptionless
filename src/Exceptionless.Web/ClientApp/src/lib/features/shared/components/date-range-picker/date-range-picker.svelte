<script lang="ts">
    import type { DateRange } from 'bits-ui';

    import { CalendarDateTime, type DateValue, fromDate, getLocalTimeZone, toCalendarDate, toCalendarDateTime, today } from '@internationalized/date';
    import Check from '@lucide/svelte/icons/check';
    import ChevronRight from '@lucide/svelte/icons/chevron-right';
    import { untrack } from 'svelte';

    import DateTime from '#comp/formatters/date-time.svelte';
    import { Button } from '#comp/ui/button/index.ts';
    import * as Field from '#comp/ui/field/index.ts';
    import { Input } from '#comp/ui/input/index.ts';
    import { RangeCalendar } from '#comp/ui/range-calendar/index.ts';
    import * as Tabs from '#comp/ui/tabs/index.ts';
    import { extractRangeExpressions, parseDateMath } from '#features/shared/utils/datemath.ts';

    import DateTimeField from './date-time-field.svelte';

    type Mode = 'absolute' | 'relative';

    type Props = {
        cancel?: () => void;
        class?: string;
        onselect?: (value: string) => void;
        value?: Date | string;
    };

    let { cancel, class: className, onselect, value = $bindable() }: Props = $props();
    const id = $props.id();

    // Simplified quick ranges — just the most commonly used
    const commonRanges = [
        {
            label: 'Last 15 minutes',
            value: '[now-15m TO now]'
        },
        {
            label: 'Last 1 hour',
            value: '[now-1h TO now]'
        },
        {
            label: 'Last 4 hours',
            value: '[now-4h TO now]'
        },
        {
            label: 'Last 24 hours',
            value: '[now-1d TO now]'
        },
        {
            label: 'Last 7 days',
            value: '[now-7d TO now]'
        },
        {
            label: 'Last 30 days',
            value: '[now-30d TO now]'
        },
        {
            label: 'Last 90 days',
            value: '[now-90d TO now]'
        }
    ];

    const timeZone = getLocalTimeZone();
    const locale = typeof navigator === 'undefined' ? undefined : navigator.language;
    const inputError = 'Enter a year, month, date, timestamp, or relative time such as now-1h.';

    let mode = $state<Mode>('absolute');
    let showCustom = $state(false);
    // Both tabs edit the same From and To expressions. An absolute field replaces only its own
    // bound, so the other bound keeps its original expression, such as now or *.
    let startValue = $state('');
    let endValue = $state('');
    let originalStart = $state('');
    let originalEnd = $state('');
    let startDateTime = $state<CalendarDateTime>();
    let endDateTime = $state<CalendarDateTime>();
    let calendarRange = $state<DateRange>({
        end: undefined,
        start: undefined
    });

    // Keep both editors synchronized with the persisted range. Relative ranges also prefill the
    // absolute fields with their resolved dates so they can be adjusted from there.
    $effect(() => {
        const currentValue = value;
        untrack(() => syncFromValue(currentValue));
    });

    const resolvedRange = $derived.by(() => {
        const referenceTime = new Date();
        return {
            end: parseDateMath(endValue, referenceTime, true),
            start: parseDateMath(startValue, referenceTime)
        };
    });
    const startValidation = $derived(resolvedRange.start);
    const endValidation = $derived(resolvedRange.end);
    const rangeError = $derived(
        startValidation.success && endValidation.success && startValidation.date > endValidation.date ? 'End must be on or after start.' : undefined
    );
    const isCustomValid = $derived(startValidation.success && endValidation.success && !rangeError);
    const isCustomValue = $derived(!!value && !commonRanges.some((range) => range.value === value));
    const isEdited = $derived(startValue.trim() !== originalStart.trim() || endValue.trim() !== originalEnd.trim());

    export function apply() {
        if (isEdited && isCustomValid) {
            applyCustom();
        }
    }

    function applyCustom() {
        if (!isCustomValid) {
            return;
        }

        // Without edits, keep the original value as written.
        const customValue = isEdited || typeof value !== 'string' || !value ? `[${startValue.trim()} TO ${endValue.trim()}]` : value;

        // Applied expressions are no longer edits, so closing the popover doesn't apply them again.
        originalStart = startValue;
        originalEnd = endValue;
        value = customValue;
        onselect?.(customValue);
    }

    /** Whole-day bounds collapse to a calendar date so the API applies its day boundaries. */
    function formatBound(dateTime: CalendarDateTime, isUpperLimit: boolean): string {
        const date = toCalendarDate(dateTime).toString();
        const isDayBoundary = isUpperLimit ? dateTime.hour === 23 && dateTime.minute === 59 : dateTime.hour === 0 && dateTime.minute === 0;
        if (isDayBoundary) {
            return date;
        }

        return `${date}T${String(dateTime.hour).padStart(2, '0')}:${String(dateTime.minute).padStart(2, '0')}`;
    }

    /** The first click selects that whole day; a second click extends the range. */
    function handleCalendarChange(range: DateRange) {
        if (!range.start) {
            return;
        }

        const end = range.end ?? range.start;
        startDateTime = new CalendarDateTime(range.start.year, range.start.month, range.start.day, 0, 0);
        endDateTime = new CalendarDateTime(end.year, end.month, end.day, 23, 59);
        startValue = formatBound(startDateTime, false);
        endValue = formatBound(endDateTime, true);
    }

    function handleEndChange(dateValue: DateValue | undefined) {
        endDateTime = dateValue ? toCalendarDateTime(dateValue) : undefined;
        endValue = endDateTime ? formatBound(endDateTime, true) : '';
        syncCalendar();
    }

    function handleKeyDown(event: KeyboardEvent) {
        if (event.key === 'Enter' && isCustomValid) {
            event.preventDefault();
            applyCustom();
        } else if (event.key === 'Escape') {
            event.preventDefault();
            cancel?.();
        }
    }

    /** The absolute fields can't show an expression that doesn't parse, so switching to them puts back the original one. */
    function handleModeChange(nextMode: string) {
        if (nextMode === 'absolute') {
            if (!startValidation.success) {
                startValue = originalStart;
            }

            if (!endValidation.success) {
                endValue = originalEnd;
            }

            setAbsoluteRangeFromExpressions(startValue, endValue);
        }

        mode = nextMode === 'relative' ? 'relative' : 'absolute';
    }

    function handleStartChange(dateValue: DateValue | undefined) {
        startDateTime = dateValue ? toCalendarDateTime(dateValue) : undefined;
        startValue = startDateTime ? formatBound(startDateTime, false) : '';
        syncCalendar();
    }

    function isRelativeExpression(...expressions: string[]): boolean {
        return expressions.some((expression) => /now/i.test(expression));
    }

    function selectRange(rangeValue: string) {
        value = rangeValue;
        onselect?.(rangeValue);
    }

    function setAbsoluteRange(start: CalendarDateTime | undefined, end: CalendarDateTime | undefined) {
        startDateTime = start;
        endDateTime = end;
        syncCalendar();
    }

    function setAbsoluteRangeFromExpressions(start: string, end: string) {
        const referenceTime = new Date();
        const startResult = parseDateMath(start, referenceTime);
        const endResult = parseDateMath(end, referenceTime, true);
        setAbsoluteRange(startResult.success ? toLocalDateTime(startResult.date) : undefined, endResult.success ? toLocalDateTime(endResult.date) : undefined);
    }

    /** The calendar shows the month of the range's start. */
    function syncCalendar() {
        calendarRange = {
            end: endDateTime ? toCalendarDate(endDateTime) : undefined,
            start: startDateTime ? toCalendarDate(startDateTime) : undefined
        };
    }

    function syncFromValue(currentValue: Date | string | undefined) {
        const range = typeof currentValue === 'string' ? extractRangeExpressions(currentValue) : null;
        if (range) {
            startValue = range.start ?? '';
            endValue = range.end ?? '';

            const isCommonRange = commonRanges.some((r) => r.value === currentValue);
            mode = !isCommonRange && isRelativeExpression(startValue, endValue) ? 'relative' : 'absolute';
            showCustom = !isCommonRange;
            setAbsoluteRangeFromExpressions(startValue, endValue);
        } else if (!currentValue) {
            startValue = '';
            endValue = '';
            mode = 'absolute';
            showCustom = false;
            setAbsoluteRange(undefined, undefined);
        }

        originalStart = startValue;
        originalEnd = endValue;
    }

    function toLocalDateTime(date: Date): CalendarDateTime {
        return toCalendarDateTime(fromDate(date, timeZone)).set({
            millisecond: 0,
            second: 0
        });
    }
</script>

<!-- Quick ranges apply on click. Custom ranges are edited in a separate panel that applies with the Apply button. -->
<div class={['flex flex-col sm:flex-row', className]}>
    <div class={['grid grid-cols-2 p-1 sm:flex sm:w-44 sm:shrink-0 sm:flex-col', showCustom && 'border-b sm:border-r sm:border-b-0']}>
        {#each commonRanges as range (range.value)}
            <button
                type="button"
                class="hover:bg-muted hover:text-foreground flex items-center gap-2 rounded-sm px-2 py-1.5 text-sm outline-hidden transition-colors select-none"
                onclick={() => selectRange(range.value)}
            >
                <span class="size-4 shrink-0">
                    {#if range.value === value}
                        <Check class="text-primary size-4" />
                    {/if}
                </span>
                <span class={range.value === value ? 'font-medium' : ''}>{range.label}</span>
            </button>
        {/each}
        <button
            type="button"
            class={[
                'hover:bg-muted hover:text-foreground flex items-center gap-2 rounded-sm px-2 py-1.5 text-sm outline-hidden transition-colors select-none',
                showCustom && 'bg-muted text-foreground'
            ]}
            aria-controls={`${id}-custom-range`}
            aria-expanded={showCustom}
            onclick={() => (showCustom = !showCustom)}
        >
            <span class="size-4 shrink-0">
                {#if isCustomValue}
                    <Check class="text-primary size-4" />
                {/if}
            </span>
            <span class={isCustomValue ? 'font-medium' : ''}>Custom range</span>
            <ChevronRight class="text-muted-foreground ml-auto size-4 shrink-0" aria-hidden="true" />
        </button>
    </div>

    {#if showCustom}
        <div id={`${id}-custom-range`} class="flex w-72 flex-col gap-3 p-3">
            <Tabs.Root value={mode} onValueChange={handleModeChange} class="gap-3">
                <Tabs.List class="grid w-full grid-cols-2">
                    <Tabs.Trigger value="absolute">Absolute</Tabs.Trigger>
                    <Tabs.Trigger value="relative">Relative</Tabs.Trigger>
                </Tabs.List>

                <Tabs.Content value="absolute" class="flex flex-col gap-3">
                    <div class="flex flex-col gap-2">
                        <DateTimeField
                            label="From"
                            {locale}
                            value={startDateTime}
                            invalid={!!rangeError}
                            onValueChange={handleStartChange}
                            onkeydown={handleKeyDown}
                        />
                        <DateTimeField
                            label="To"
                            {locale}
                            value={endDateTime}
                            invalid={!!rangeError}
                            onValueChange={handleEndChange}
                            onkeydown={handleKeyDown}
                        />
                        {#if rangeError}
                            <Field.FieldError>{rangeError}</Field.FieldError>
                        {/if}
                    </div>
                    <RangeCalendar
                        bind:value={calendarRange}
                        class="mx-auto p-0"
                        {locale}
                        maxValue={today(timeZone)}
                        onValueChange={handleCalendarChange}
                        preventDeselect
                    />
                    <p class="text-muted-foreground text-xs">Times are in your local time zone ({timeZone}).</p>
                </Tabs.Content>

                <Tabs.Content value="relative">
                    <Field.FieldGroup id={`${id}-custom`} class="gap-3">
                        <Field.FieldDescription id={`${id}-help`}>
                            Use now-1h, now-7d, or now/d for the start of today. Dates such as 2024 or 2024-01 cover the whole year or month.
                        </Field.FieldDescription>
                        <Field.Field class="gap-1" data-invalid={!!startValue && !startValidation.success}>
                            <Field.FieldLabel for={`${id}-start`}>From</Field.FieldLabel>
                            <Input
                                id={`${id}-start`}
                                placeholder="e.g. now-1h or 2024-01-01"
                                class="h-8 font-mono text-sm"
                                bind:value={startValue}
                                aria-invalid={startValue ? !startValidation.success : undefined}
                                aria-describedby={`${id}-help ${id}-start-status`}
                                onkeydown={handleKeyDown}
                            />
                            <div id={`${id}-start-status`} aria-live="polite">
                                {#if startValue && startValidation.success}
                                    <p class="text-muted-foreground text-xs"><DateTime value={startValidation.date} /></p>
                                {:else if startValue}
                                    <Field.FieldError>{inputError}</Field.FieldError>
                                {/if}
                            </div>
                        </Field.Field>
                        <Field.Field class="gap-1" data-invalid={(!!endValue && !endValidation.success) || !!rangeError}>
                            <Field.FieldLabel for={`${id}-end`}>To</Field.FieldLabel>
                            <Input
                                id={`${id}-end`}
                                placeholder="e.g. now or 2024-01-31"
                                class="h-8 font-mono text-sm"
                                bind:value={endValue}
                                aria-invalid={endValue ? !endValidation.success || !!rangeError : undefined}
                                aria-describedby={`${id}-help ${id}-end-status`}
                                onkeydown={handleKeyDown}
                            />
                            <div id={`${id}-end-status`} aria-live="polite">
                                {#if endValue && endValidation.success}
                                    <p class="text-muted-foreground text-xs"><DateTime value={endValidation.date} /></p>
                                {:else if endValue}
                                    <Field.FieldError>{inputError}</Field.FieldError>
                                {/if}
                                {#if rangeError}
                                    <Field.FieldError>{rangeError}</Field.FieldError>
                                {/if}
                            </div>
                        </Field.Field>
                    </Field.FieldGroup>
                </Tabs.Content>
            </Tabs.Root>

            <Button size="sm" class="w-full" disabled={!isCustomValid} onclick={applyCustom}>Apply</Button>
        </div>
    {/if}
</div>
