<script lang="ts">
    import type { DateValue } from '@internationalized/date';

    import { DateField } from 'bits-ui';

    type Props = {
        invalid?: boolean;
        label: string;
        locale?: string;
        onkeydown?: (event: KeyboardEvent) => void;
        onValueChange?: (value: DateValue | undefined) => void;
        placeholder?: DateValue;
        value?: DateValue;
    };

    let { invalid = false, label, locale, onkeydown, onValueChange, placeholder, value = $bindable() }: Props = $props();
</script>

<DateField.Root bind:value granularity="minute" hideTimeZone {locale} {onValueChange} {placeholder}>
    <div class="flex flex-col gap-1">
        <DateField.Label class="text-sm font-medium">{label}</DateField.Label>
        <DateField.Input
            aria-invalid={invalid || undefined}
            class="border-input dark:bg-input/30 focus-within:border-ring focus-within:ring-ring/50 aria-invalid:border-destructive aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40 flex h-8 w-full items-center rounded-lg border bg-transparent px-2 font-mono text-sm tabular-nums transition-colors outline-none focus-within:ring-3 aria-invalid:ring-3"
            {onkeydown}
        >
            {#snippet children({ segments })}
                {#each segments as { part, value: segmentValue }, index (index)}
                    {#if part === 'literal'}
                        <DateField.Segment {part} class="text-muted-foreground">{segmentValue}</DateField.Segment>
                    {:else}
                        <DateField.Segment
                            {part}
                            class="focus:bg-primary focus:text-primary-foreground data-placeholder:text-muted-foreground rounded-sm px-0.5 outline-none"
                        >
                            {segmentValue}
                        </DateField.Segment>
                    {/if}
                {/each}
            {/snippet}
        </DateField.Input>
    </div>
</DateField.Root>
