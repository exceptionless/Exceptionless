<script lang="ts">
    import { type QuickRangeOption, quickRanges } from '$features/shared/components/date-range-picker/quick-ranges';
    import { formatDateLabel } from '$features/shared/dates';
    import { extractRangeExpressions, TIME_UNIT_NAMES } from '$features/shared/utils/datemath';
    import { parseDateMathRange } from '$features/shared/utils/datemath';

    interface Props {
        value?: Date | null | string | undefined;
    }

    let { value }: Props = $props();

    const quickRangeOptions: QuickRangeOption[] = quickRanges.flatMap((section) => section.options);

    function getQuickRangeLabel(value: string) {
        const normalized = value.trim();
        const match = quickRangeOptions.find((option) => option.value === normalized);
        return match?.label;
    }

    function getRelativeTimeLabel(start: string, end: string): null | string {
        // Pattern: "now-{number}{unit} TO now" -> "Last {number} {unit}"
        if (end.trim() === 'now') {
            const relativeMatch = start.trim().match(/^now-(\d+)([dhmswyMQ])$/);
            if (relativeMatch) {
                const amount = relativeMatch[1];
                const unit = relativeMatch[2];
                if (amount && unit) {
                    const unitName = TIME_UNIT_NAMES[unit as keyof typeof TIME_UNIT_NAMES] as string;
                    if (unitName) {
                        const count = parseInt(amount, 10);
                        const pluralUnit = count === 1 ? unitName.slice(0, -1) : unitName; // Remove 's' for singular
                        return `Last ${count} ${pluralUnit}`;
                    }
                }
            }
        }

        // Pattern: "now TO now+{number}{unit}" -> "Next {number} {unit}"
        if (start.trim() === 'now') {
            const futureMatch = end.trim().match(/^now\+(\d+)([dhmswyMQ])$/);
            if (futureMatch) {
                const amount = futureMatch[1];
                const unit = futureMatch[2];
                if (amount && unit) {
                    const unitName = TIME_UNIT_NAMES[unit as keyof typeof TIME_UNIT_NAMES] as string;
                    if (unitName) {
                        const count = parseInt(amount, 10);
                        const pluralUnit = count === 1 ? unitName.slice(0, -1) : unitName; // Remove 's' for singular
                        return `Next ${count} ${pluralUnit}`;
                    }
                }
            }
        }

        return null;
    }

    // TODO: See if this can be simplified
    const displayLabel = $derived.by(() => {
        if (value === undefined || value === null) {
            return 'No Value';
        }

        if (value instanceof Date) {
            return formatDateLabel(value);
        }

        if (typeof value === 'string') {
            const trimmed = value.trim();

            if (trimmed === '') {
                return 'All Time';
            }

            // Check if it's a quick range first
            const quickRangeLabel = getQuickRangeLabel(trimmed);
            if (quickRangeLabel) {
                return quickRangeLabel;
            }

            // Try to extract range expressions for relative time detection
            const rangeExpressions = extractRangeExpressions(trimmed);
            if (rangeExpressions && rangeExpressions.start && rangeExpressions.end) {
                const relativeLabel = getRelativeTimeLabel(rangeExpressions.start, rangeExpressions.end);
                if (relativeLabel) {
                    return relativeLabel;
                }
            }

            // Try to parse as a time parameter (date range expression)
            try {
                const { end, start } = parseDateMathRange(trimmed);
                return `${formatDateLabel(start)} to ${formatDateLabel(end)}`;
            } catch {
                // If parsing fails, just return the original value
                return trimmed;
            }
        }

        return String(value);
    });
</script>

{displayLabel}
