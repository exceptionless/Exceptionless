<script lang="ts">
    import ErrorMessage from '$comp/error-message.svelte';
    import NumberFormatter from '$comp/formatters/number.svelte';
    import * as Alert from '$comp/ui/alert';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import * as Select from '$comp/ui/select';
    import { Spinner } from '$comp/ui/spinner';
    import { updateProject } from '$features/projects/api.svelte';
    import { createProjectIngestLimit, getEffectiveProjectLimit, type ProjectBudgetType } from '$features/projects/budget-utils';
    import { ProjectIngestLimitType, type ViewProject } from '$features/projects/models';
    import { type ProjectBudgetCardFormData, ProjectBudgetCardSchema } from '$features/projects/schemas';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import { createForm } from '@tanstack/svelte-form';
    import { untrack } from 'svelte';
    import { toast } from 'svelte-sonner';

    let { organizationLimit, project }: { organizationLimit: number; project: ViewProject } = $props();

    const initialProject = untrack(() => project);
    const initialType: ProjectBudgetType = !initialProject.ingest_limit
        ? 'none'
        : initialProject.ingest_limit.type === ProjectIngestLimitType.Fixed
          ? 'fixed'
          : 'percent';
    const initialValue =
        initialType === 'fixed'
            ? (initialProject.ingest_limit?.fixed_limit?.toString() ?? '')
            : initialType === 'percent'
              ? (initialProject.ingest_limit?.percent_of_organization_limit?.toString() ?? '')
              : '';

    let selectedType = $state<ProjectBudgetType>(initialType);
    const update = updateProject({
        route: {
            get id() {
                return project.id;
            }
        }
    });

    const form = createForm(() => ({
        defaultValues: { type: initialType, value: initialValue } as ProjectBudgetCardFormData,
        validators: {
            onSubmit: ProjectBudgetCardSchema,
            onSubmitAsync: async ({ value }) => {
                if (value.type === 'percent' && organizationLimit < 0) {
                    return { form: 'Percentage limits require a finite organization allowance.' };
                }

                const ingestLimit = createProjectIngestLimit(value.type, value.value);

                try {
                    await update.mutateAsync({ ingest_limit: ingestLimit });
                    toast.success('Project event budget saved.');
                    return null;
                } catch (error: unknown) {
                    toast.error('Unable to save the project event budget.');
                    return error instanceof ProblemDetails ? problemDetailsToFormErrors(error) : { form: 'An unexpected error occurred.' };
                }
            }
        }
    }));
</script>

<Card.Root>
    <Card.Header>
        <Card.Title>Project event budget</Card.Title>
        <Card.Description>Optionally cap accepted events for this project without changing the organization allowance.</Card.Description>
    </Card.Header>
    <form
        onsubmit={(event) => {
            event.preventDefault();
            event.stopPropagation();
            form.handleSubmit();
        }}
    >
        <Card.Content>
            <form.Subscribe selector={(state) => state.errors}>
                {#snippet children(errors)}
                    <ErrorMessage message={getFormErrorMessages(errors)} />
                {/snippet}
            </form.Subscribe>

            <Field.Group>
                <form.Field name="type">
                    {#snippet children(field)}
                        <Field.Field>
                            <Field.Label for={field.name}>Limit type</Field.Label>
                            <Select.Root
                                type="single"
                                value={field.state.value}
                                onValueChange={(value) => {
                                    const nextType = value as ProjectBudgetType;
                                    selectedType = nextType;
                                    field.handleChange(nextType);
                                    if (nextType === 'none') {
                                        form.setFieldValue('value', '');
                                    }
                                }}
                                disabled={update.isPending}
                            >
                                <Select.Trigger id={field.name} class="w-full sm:w-64">
                                    {selectedType === 'none'
                                        ? 'No project limit'
                                        : selectedType === 'fixed'
                                          ? 'Fixed event count'
                                          : 'Percentage of organization allowance'}
                                </Select.Trigger>
                                <Select.Content>
                                    <Select.Group>
                                        <Select.Item value="none">No project limit</Select.Item>
                                        <Select.Item value="fixed">Fixed event count</Select.Item>
                                        <Select.Item value="percent" disabled={organizationLimit < 0}>Percentage of organization allowance</Select.Item>
                                    </Select.Group>
                                </Select.Content>
                            </Select.Root>
                        </Field.Field>
                    {/snippet}
                </form.Field>

                {#if selectedType !== 'none'}
                    <form.Field name="value">
                        {#snippet children(field)}
                            <Field.Field data-invalid={ariaInvalid(field)}>
                                <Field.Label for={field.name}>{selectedType === 'fixed' ? 'Monthly event count' : 'Percentage'}</Field.Label>
                                <Input
                                    id={field.name}
                                    name={field.name}
                                    inputmode={selectedType === 'fixed' ? 'numeric' : 'decimal'}
                                    placeholder={selectedType === 'fixed' ? '100000' : '50'}
                                    value={field.state.value}
                                    onblur={field.handleBlur}
                                    oninput={(event) => field.handleChange(event.currentTarget.value)}
                                    aria-invalid={ariaInvalid(field)}
                                    disabled={update.isPending}
                                />
                                <Field.Description>
                                    {selectedType === 'fixed'
                                        ? 'Use a whole number greater than 0. The effective cap cannot exceed a finite organization allowance.'
                                        : 'Use a value greater than 0 and no more than 100.'}
                                </Field.Description>
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
                        {/snippet}
                    </form.Field>
                {/if}

                <form.Subscribe selector={(state) => state.values}>
                    {#snippet children(values)}
                        {@const draftLimit = createProjectIngestLimit(values.type, values.value)}
                        {@const effectiveLimit = getEffectiveProjectLimit(organizationLimit, draftLimit)}
                        <p class="text-muted-foreground text-sm" aria-live="polite">
                            Effective project limit:
                            {#if effectiveLimit == null}
                                organization allowance
                            {:else}
                                <NumberFormatter value={effectiveLimit} /> events per month
                            {/if}
                        </p>
                        {#if values.type === 'fixed' && organizationLimit >= 0 && (draftLimit?.fixed_limit ?? 0) > organizationLimit}
                            <Alert.Root role="status">
                                <Alert.Title>Fixed limit will be clamped</Alert.Title>
                                <Alert.Description>The effective cap cannot exceed the organization's finite event allowance.</Alert.Description>
                            </Alert.Root>
                        {/if}
                    {/snippet}
                </form.Subscribe>

                {#if project.is_smart_throttled}
                    <Alert.Root role="status">
                        <Alert.Title class="flex items-center gap-2">Smart throttling active <Badge variant="secondary">5% sample</Badge></Alert.Title>
                        <Alert.Description>This project is being sampled independently; other projects remain unaffected.</Alert.Description>
                    </Alert.Root>
                {/if}
            </Field.Group>
        </Card.Content>
        <Card.Footer class="justify-end">
            <form.Subscribe selector={(state) => state.isSubmitting}>
                {#snippet children(isSubmitting)}
                    <Button type="submit" disabled={isSubmitting || update.isPending}>
                        {#if isSubmitting || update.isPending}<Spinner />{/if}
                        Save project budget
                    </Button>
                {/snippet}
            </form.Subscribe>
        </Card.Footer>
    </form>
</Card.Root>
