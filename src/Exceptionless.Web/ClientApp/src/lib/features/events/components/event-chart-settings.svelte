<script lang="ts">
    import type { EventChart } from '$generated/api';

    import ErrorMessage from '$comp/error-message.svelte';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import * as Select from '$comp/ui/select';
    import { organization } from '$features/organizations/context.svelte';
    import { ariaInvalid, mapFieldErrors } from '$shared/validation';
    import { createForm } from '@tanstack/svelte-form';
    import { untrack } from 'svelte';

    import { getEventMeasurementsQuery } from '../api.svelte';
    import { eventChartSchema } from '../event-chart';

    let {
        chart,
        filter,
        onApply,
        time
    }: {
        chart: EventChart | null;
        filter?: null | string;
        onApply: (chart: EventChart | null) => void;
        time?: null | string;
    } = $props();
    let open = $state(false);

    const catalog = getEventMeasurementsQuery({
        get enabled() {
            return open;
        },
        get filter() {
            return filter;
        },
        get organizationId() {
            return organization.current;
        },
        get time() {
            return time;
        }
    });

    function defaults(): EventChart {
        return (
            chart ?? {
                aggregation: 'avg',
                display: 'line',
                group_by: 'source',
                measurement: '',
                mode: 'buckets',
                unit: ''
            }
        );
    }
    function normalize(value: EventChart): EventChart {
        return {
            ...value,
            group_by: value.group_by || null,
            measurement: value.measurement || null,
            unit: value.unit || null
        };
    }
    const form = createForm(() => ({
        defaultValues: defaults(),
        onSubmit: ({ value }) => {
            onApply(normalize(value));
            open = false;
        },
        validators: {
            onSubmit: ({ value }) => {
                const result = eventChartSchema.safeParse(normalize(value));
                return result.success
                    ? undefined
                    : {
                          fields: Object.fromEntries(result.error.issues.map((issue) => [issue.path[0], issue.message]))
                      };
            }
        }
    }));

    $effect(() => {
        if (open) {
            untrack(() => form.reset(defaults()));
        }
    });

    const textFields = [
        {
            key: 'measurement',
            label: 'Measurement',
            list: 'event-measurement-names',
            placeholder: 'duration'
        },
        {
            key: 'unit',
            label: 'Unit',
            list: 'event-measurement-units',
            placeholder: 'ms'
        },
        {
            key: 'group_by',
            label: 'Split by',
            list: 'event-measurement-groups',
            placeholder: 'source, stack, outcome, or dimensions.branch'
        }
    ] as const;
    const selectFields = [
        {
            key: 'aggregation',
            label: 'Aggregation',
            options: [
                {
                    label: 'Average',
                    value: 'avg'
                },
                {
                    label: 'Minimum',
                    value: 'min'
                },
                {
                    label: 'Maximum',
                    value: 'max'
                },
                {
                    label: 'Sum',
                    value: 'sum'
                },
                {
                    label: '50th percentile',
                    value: 'p50'
                },
                {
                    label: '95th percentile',
                    value: 'p95'
                },
                {
                    label: '99th percentile',
                    value: 'p99'
                },
                {
                    label: 'Count',
                    value: 'count'
                }
            ]
        },
        {
            key: 'mode',
            label: 'Observations',
            options: [
                {
                    label: 'Time buckets',
                    value: 'buckets'
                },
                {
                    label: 'Individual events',
                    value: 'events'
                }
            ]
        },
        {
            key: 'display',
            label: 'Display',
            options: [
                {
                    label: 'Line',
                    value: 'line'
                },
                {
                    label: 'Bar',
                    value: 'bar'
                }
            ]
        }
    ] as const;
</script>

<Dialog.Root bind:open>
    <Dialog.Trigger>
        {#snippet child({ props })}<Button {...props} variant="outline" size="sm">Configure chart</Button>{/snippet}
    </Dialog.Trigger>
    <Dialog.Content>
        <Dialog.Header>
            <Dialog.Title>Configure chart</Dialog.Title>
            <Dialog.Description>Chart a measurement in these events. Save the view to keep this configuration.</Dialog.Description>
        </Dialog.Header>
        <form
            onsubmit={(event) => {
                event.preventDefault();
                event.stopPropagation();
                void form.handleSubmit();
            }}
        >
            <Field.FieldGroup>
                {#each textFields as item (item.key)}
                    <form.Field name={item.key}>
                        {#snippet children(field)}
                            <Field.Field data-invalid={ariaInvalid(field)}>
                                <Field.FieldLabel for={`chart-${item.key}`}>{item.label}</Field.FieldLabel>
                                <Input
                                    id={`chart-${item.key}`}
                                    value={field.state.value ?? ''}
                                    placeholder={item.placeholder}
                                    list={item.list}
                                    oninput={(event) => field.handleChange(event.currentTarget.value)}
                                    onblur={field.handleBlur}
                                    aria-invalid={ariaInvalid(field)}
                                    aria-describedby={`chart-${item.key}-error`}
                                />
                                <Field.FieldError id={`chart-${item.key}-error`} errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
                        {/snippet}
                    </form.Field>
                {/each}
                {#each selectFields as item (item.key)}
                    <form.Field name={item.key}>
                        {#snippet children(field)}
                            <Field.Field>
                                <Field.FieldLabel for={`chart-${item.key}`}>{item.label}</Field.FieldLabel>
                                <Select.Root type="single" value={field.state.value} onValueChange={field.handleChange}>
                                    <Select.Trigger id={`chart-${item.key}`}
                                        >{item.options.find((option) => option.value === field.state.value)?.label}</Select.Trigger
                                    >
                                    <Select.Content
                                        ><Select.Group
                                            >{#each item.options as option (option.value)}<Select.Item value={option.value}>{option.label}</Select.Item
                                                >{/each}</Select.Group
                                        ></Select.Content
                                    >
                                </Select.Root>
                            </Field.Field>
                        {/snippet}
                    </form.Field>
                {/each}
                <Field.FieldDescription
                    >Units are exact: ms and s are separate series. Individual events use their recorded values. To count all events, clear Measurement and Unit
                    and select Count with Time buckets.</Field.FieldDescription
                >
                {#if catalog.error}<ErrorMessage message={catalog.error.title ?? 'Unable to load available measurements.'} />{/if}
                {#if catalog.data?.truncated}<p class="text-muted-foreground text-sm">
                        Only the most common measurements are suggested. You can enter another name and unit.
                    </p>{/if}
            </Field.FieldGroup>
            <Dialog.Footer class="mt-4">
                <Button
                    variant="outline"
                    type="button"
                    onclick={() => {
                        onApply(null);
                        open = false;
                    }}>Default chart</Button
                >
                <Button type="submit">Apply</Button>
            </Dialog.Footer>
        </form>
    </Dialog.Content>
</Dialog.Root>
<datalist id="event-measurement-names"
    >{#each [...new Set(catalog.data?.measurements.map((m) => m.name) ?? [])] as name (name)}<option value={name}></option>{/each}</datalist
>
<datalist id="event-measurement-units"
    >{#each [...new Set(catalog.data?.measurements.map((m) => m.unit) ?? [])] as unit (unit)}<option value={unit}></option>{/each}</datalist
>
<datalist id="event-measurement-groups"><option value="source"></option><option value="stack"></option><option value="outcome"></option></datalist>
