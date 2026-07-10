<script lang="ts">
    import type { ViewRateNotificationRule } from '$features/rate-notifications/types';

    import ErrorMessage from '$comp/error-message.svelte';
    import { A, Muted } from '$comp/typography';
    import * as Alert from '$comp/ui/alert';
    import { Button } from '$comp/ui/button';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import * as Select from '$comp/ui/select';
    import { Spinner } from '$comp/ui/spinner';
    import { Switch } from '$comp/ui/switch';
    import { postRateNotificationRule, putRateNotificationRule } from '$features/rate-notifications/api.svelte';
    import { getRateNotificationRuleFormData, RateNotificationRuleSchema, toRateNotificationRuleRequest } from '$features/rate-notifications/schemas';
    import {
        COOLDOWN_OPTIONS,
        RateNotificationSignal,
        RateNotificationSubject,
        SIGNAL_LABELS,
        SUBJECT_LABELS,
        WINDOW_OPTIONS
    } from '$features/rate-notifications/types';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { getProjectStacksQuery, getStackQuery } from '$features/stacks/api.svelte';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import AlertTriangleIcon from '@lucide/svelte/icons/alert-triangle';
    import InfoIcon from '@lucide/svelte/icons/info';
    import { createForm } from '@tanstack/svelte-form';
    import { untrack } from 'svelte';
    import { toast } from 'svelte-sonner';

    interface Props {
        hasPremiumFeatures?: boolean;
        onCancel?: () => void;
        onSaved?: (rule: ViewRateNotificationRule) => void;
        projectId: string | undefined;
        rule?: ViewRateNotificationRule;
        upgrade: () => Promise<void> | void;
        userId: string | undefined;
    }

    let { hasPremiumFeatures = false, onCancel, onSaved, projectId, rule, upgrade, userId }: Props = $props();

    const activeRule = $derived(rule?.project_id === projectId ? rule : undefined);
    const createMutation = postRateNotificationRule();
    const updateMutation = putRateNotificationRule();
    const stacksQuery = getProjectStacksQuery({
        params: { limit: 100, sort: 'last_occurrence:desc' },
        route: {
            get projectId() {
                return projectId;
            }
        }
    });
    const selectedStackQuery = getStackQuery({
        route: {
            get id() {
                return activeRule?.stack_id ?? undefined;
            }
        }
    });

    const isEditing = $derived(!!activeRule);
    const stacks = $derived.by(() => {
        const projectStacks = stacksQuery.data?.data ?? [];
        const selectedStack = selectedStackQuery.data;
        return selectedStack && !projectStacks.some((stack) => stack.id === selectedStack.id) ? [selectedStack, ...projectStacks] : projectStacks;
    });

    const form = createForm(() => ({
        defaultValues: getRateNotificationRuleFormData(activeRule),
        validators: {
            onSubmit: RateNotificationRuleSchema,
            onSubmitAsync: async ({ value }) => {
                if (!hasPremiumFeatures) {
                    return { form: 'A premium plan is required to enable rate notifications.' };
                }

                if (!projectId || !userId) {
                    return { form: 'The selected project or user is unavailable. Please try again.' };
                }

                try {
                    const request = toRateNotificationRuleRequest(value, hasPremiumFeatures);
                    const route = { projectId, userId };
                    const saved = activeRule
                        ? await updateMutation.mutateAsync({ ...route, body: request, ruleId: activeRule.id })
                        : await createMutation.mutateAsync({ ...route, body: request });
                    toast.success(activeRule ? 'Rule updated.' : 'Rule created.');
                    onSaved?.(saved);
                    return null;
                } catch (error: unknown) {
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }

                    return { form: 'Failed to save rule. Please try again.' };
                }
            }
        }
    }));

    $effect(() => {
        const selectedProjectId = projectId;
        const selectedRule = activeRule;
        untrack(() => {
            void selectedProjectId;
            setFormValues(selectedRule);
        });
    });

    function setFormValues(value: undefined | ViewRateNotificationRule) {
        const values = getRateNotificationRuleFormData(value);
        form.reset();
        form.setFieldValue('cooldown', values.cooldown);
        form.setFieldValue('is_enabled', hasPremiumFeatures ? values.is_enabled : false);
        form.setFieldValue('name', values.name);
        form.setFieldValue('signal', values.signal);
        form.setFieldValue('stack_id', values.stack_id);
        form.setFieldValue('subject', values.subject);
        form.setFieldValue('threshold', values.threshold);
        form.setFieldValue('window', values.window);
    }
</script>

<form
    class="flex flex-col gap-5"
    onsubmit={(event) => {
        event.preventDefault();
        form.handleSubmit();
    }}
>
    <form.Subscribe selector={(state) => state.errors}>
        {#snippet children(errors)}
            <ErrorMessage message={getFormErrorMessages(errors)} />
        {/snippet}
    </form.Subscribe>

    {#if !hasPremiumFeatures}
        <Alert.Root variant="information">
            <InfoIcon aria-hidden="true" />
            <Alert.Title><A onclick={upgrade}>Upgrade now</A> to enable and create rate notification rules.</Alert.Title>
        </Alert.Root>
    {/if}

    <Alert.Root variant="information">
        <AlertTriangleIcon aria-hidden="true" />
        <Alert.Title>This rule may be noisy. Use a cooldown to avoid repeated emails.</Alert.Title>
    </Alert.Root>

    <Field.FieldGroup>
        <form.Field name="name">
            {#snippet children(field)}
                <Field.Field data-invalid={ariaInvalid(field)}>
                    <Field.Label for={field.name}>Name</Field.Label>
                    <Input
                        id={field.name}
                        value={field.state.value}
                        maxlength={100}
                        placeholder="e.g. Production error storm"
                        onblur={field.handleBlur}
                        oninput={(event) => field.handleChange(event.currentTarget.value)}
                        aria-invalid={ariaInvalid(field)}
                    />
                    <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                </Field.Field>
            {/snippet}
        </form.Field>

        <form.Field name="signal">
            {#snippet children(field)}
                <Field.Field data-invalid={ariaInvalid(field)}>
                    <Field.Label for={field.name}>Signal</Field.Label>
                    <Select.Root
                        type="single"
                        value={field.state.value}
                        onValueChange={(value) => value && field.handleChange(value as RateNotificationSignal)}
                    >
                        <Select.Trigger id={field.name} class="w-full">{SIGNAL_LABELS[field.state.value]}</Select.Trigger>
                        <Select.Content>
                            <Select.Group>
                                {#each Object.entries(SIGNAL_LABELS) as [value, label] (value)}
                                    <Select.Item {value}>{label}</Select.Item>
                                {/each}
                            </Select.Group>
                        </Select.Content>
                    </Select.Root>
                    <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                </Field.Field>
            {/snippet}
        </form.Field>

        <form.Field name="subject">
            {#snippet children(field)}
                <Field.Field data-invalid={ariaInvalid(field)}>
                    <Field.Label for={field.name}>Subject</Field.Label>
                    <Select.Root
                        type="single"
                        value={field.state.value}
                        onValueChange={(value) => {
                            if (value) {
                                field.handleChange(value as RateNotificationSubject);
                                if (value === RateNotificationSubject.Project) {
                                    form.setFieldValue('stack_id', '');
                                }
                            }
                        }}
                    >
                        <Select.Trigger id={field.name} class="w-full">{SUBJECT_LABELS[field.state.value]}</Select.Trigger>
                        <Select.Content>
                            <Select.Group>
                                {#each Object.entries(SUBJECT_LABELS) as [value, label] (value)}
                                    <Select.Item {value}>{label}</Select.Item>
                                {/each}
                            </Select.Group>
                        </Select.Content>
                    </Select.Root>
                    <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                </Field.Field>
            {/snippet}
        </form.Field>

        <form.Subscribe selector={(state) => state.values.subject}>
            {#snippet children(subject)}
                {#if subject === RateNotificationSubject.Stack}
                    <form.Field name="stack_id">
                        {#snippet children(field)}
                            <Field.Field data-invalid={ariaInvalid(field)}>
                                <Field.Label for={field.name}>Stack</Field.Label>
                                <Select.Root type="single" value={field.state.value} onValueChange={(value) => field.handleChange(value ?? '')}>
                                    <Select.Trigger id={field.name} class="w-full" disabled={stacksQuery.isLoading}>
                                        {stacks.find((stack) => stack.id === field.state.value)?.title ??
                                            (stacksQuery.isLoading ? 'Loading stacks...' : 'Select a stack')}
                                    </Select.Trigger>
                                    <Select.Content>
                                        <Select.Group>
                                            {#each stacks as stack (stack.id)}
                                                <Select.Item value={stack.id}>{stack.title}</Select.Item>
                                            {/each}
                                        </Select.Group>
                                    </Select.Content>
                                </Select.Root>
                                <Field.Description>Choose from the 100 most recently active stacks in this project.</Field.Description>
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
                        {/snippet}
                    </form.Field>
                {/if}
            {/snippet}
        </form.Subscribe>

        <form.Field name="threshold">
            {#snippet children(field)}
                <Field.Field data-invalid={ariaInvalid(field)}>
                    <Field.Label for={field.name}>Threshold (events)</Field.Label>
                    <Input
                        id={field.name}
                        type="number"
                        value={field.state.value}
                        min={1}
                        step={1}
                        onblur={field.handleBlur}
                        oninput={(event) => field.handleChange(event.currentTarget.valueAsNumber)}
                        aria-invalid={ariaInvalid(field)}
                    />
                    <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                </Field.Field>
            {/snippet}
        </form.Field>

        <form.Field name="window">
            {#snippet children(field)}
                <Field.Field data-invalid={ariaInvalid(field)}>
                    <Field.Label for={field.name}>Window</Field.Label>
                    <Select.Root type="single" value={field.state.value} onValueChange={(value) => value && field.handleChange(value)}>
                        <Select.Trigger id={field.name} class="w-full">
                            {WINDOW_OPTIONS.find((option) => option.value === field.state.value)?.label}
                        </Select.Trigger>
                        <Select.Content>
                            <Select.Group>
                                {#each WINDOW_OPTIONS as option (option.value)}
                                    <Select.Item value={option.value}>{option.label}</Select.Item>
                                {/each}
                            </Select.Group>
                        </Select.Content>
                    </Select.Root>
                    <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                </Field.Field>
            {/snippet}
        </form.Field>

        <form.Field name="cooldown">
            {#snippet children(field)}
                <Field.Field data-invalid={ariaInvalid(field)}>
                    <Field.Label for={field.name}>Cooldown</Field.Label>
                    <Select.Root type="single" value={field.state.value} onValueChange={(value) => value && field.handleChange(value)}>
                        <Select.Trigger id={field.name} class="w-full">
                            {COOLDOWN_OPTIONS.find((option) => option.value === field.state.value)?.label}
                        </Select.Trigger>
                        <Select.Content>
                            <Select.Group>
                                {#each COOLDOWN_OPTIONS as option (option.value)}
                                    <Select.Item value={option.value}>{option.label}</Select.Item>
                                {/each}
                            </Select.Group>
                        </Select.Content>
                    </Select.Root>
                    <Field.Description>Further notifications are suppressed during this period.</Field.Description>
                    <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                </Field.Field>
            {/snippet}
        </form.Field>

        <form.Field name="is_enabled">
            {#snippet children(field)}
                <Field.Field orientation="horizontal" data-disabled={!hasPremiumFeatures}>
                    <div>
                        <Field.Label for={field.name}>Enabled</Field.Label>
                        <Muted class="text-xs">Start evaluating this rule immediately.</Muted>
                    </div>
                    <Switch id={field.name} checked={field.state.value} disabled={!hasPremiumFeatures} onCheckedChange={(value) => field.handleChange(value)} />
                </Field.Field>
            {/snippet}
        </form.Field>
    </Field.FieldGroup>

    <div class="flex justify-end gap-2">
        {#if onCancel}
            <Button variant="outline" onclick={onCancel} type="button">Cancel</Button>
        {/if}
        <form.Subscribe selector={(state) => state.isSubmitting}>
            {#snippet children(isSubmitting)}
                <Button type="submit" disabled={isSubmitting || !hasPremiumFeatures}>
                    {#if isSubmitting}
                        <Spinner data-icon="inline-start" />
                        Saving...
                    {:else}
                        {isEditing ? 'Save changes' : 'Create rule'}
                    {/if}
                </Button>
            {/snippet}
        </form.Subscribe>
    </div>
</form>
