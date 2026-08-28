<script lang="ts">
    import { resolve } from '$app/paths';
    import ErrorMessage from '$comp/error-message.svelte';
    import Currency from '$comp/formatters/currency.svelte';
    import NumberCompact from '$comp/formatters/number-compact.svelte';
    import Number from '$comp/formatters/number.svelte';
    import TimeAgo from '$comp/formatters/time-ago.svelte';
    import { Muted } from '$comp/typography';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Skeleton } from '$comp/ui/skeleton';
    import { Spinner } from '$comp/ui/spinner';
    import { Switch } from '$comp/ui/switch';
    import * as Table from '$comp/ui/table';
    import {
        getAdminAssistantSettingsQuery,
        getAdminAssistantUsageQuery,
        putAdminAssistantEnabledSettingsMutation,
        putAdminAssistantSettingsMutation
    } from '$features/admin/api.svelte';
    import { getBlockedCount, getTotalTokens, getUsageRisk, getUtcMonthKey, type UsageRisk } from '$features/admin/assistant-usage';
    import { type AssistantSettingsFormData, AssistantSettingsSchema } from '$features/admin/schemas';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@foundatiofx/fetchclient';
    import Bot from '@lucide/svelte/icons/bot';
    import Building2 from '@lucide/svelte/icons/building-2';
    import Coins from '@lucide/svelte/icons/coins';
    import Gauge from '@lucide/svelte/icons/gauge';
    import MessagesSquare from '@lucide/svelte/icons/messages-square';
    import RotateCcw from '@lucide/svelte/icons/rotate-ccw';
    import Save from '@lucide/svelte/icons/save';
    import Wrench from '@lucide/svelte/icons/wrench';
    import { createForm } from '@tanstack/svelte-form';
    import { toast } from 'svelte-sonner';

    function riskBadgeVariant(risk: UsageRisk): 'destructive' | 'outline' | 'yellow' {
        if (risk === 'critical') {
            return 'destructive';
        }

        return risk === 'warning' ? 'yellow' : 'outline';
    }

    function utilizationLabel(utilization: null | number | undefined): string {
        return utilization === null || utilization === undefined ? 'No limit' : `${Math.round(utilization * 100)}%`;
    }

    const currentMonth = getUtcMonthKey();
    const settingsQuery = getAdminAssistantSettingsQuery();
    const updateEnabledSettings = putAdminAssistantEnabledSettingsMutation();
    const updateSettings = putAdminAssistantSettingsMutation();
    let assistantEnabled = $state(false);
    let loadedAvailabilityKey = $state<null | string>(null);
    let selectedMonth = $state(currentMonth);
    let loadedSettingsKey = $state<null | string>(null);
    const usageQuery = getAdminAssistantUsageQuery(() => selectedMonth);
    const settings = $derived(settingsQuery.data);
    const availabilityKey = $derived(
        settings ? JSON.stringify([settings.enabled, settings.configured_enabled, settings.is_enabled_overridden, settings.is_configured]) : null
    );
    const settingsKey = $derived(settings ? JSON.stringify([settings.model, settings.configured_model, settings.is_overridden]) : null);
    const usage = $derived(usageQuery.data);
    const totalTokens = $derived((usage?.prompt_tokens ?? 0) + (usage?.completion_tokens ?? 0));
    const averageCost = $derived(usage && usage.active_organizations > 0 ? usage.cost_usd / usage.active_organizations : 0);
    const tokensPerTurn = $derived(usage && usage.turns > 0 ? totalTokens / usage.turns : 0);

    const settingsForm = createForm(() => ({
        defaultValues: {
            model: ''
        } as AssistantSettingsFormData,
        validators: {
            onSubmit: AssistantSettingsSchema,
            onSubmitAsync: async ({ value }) => {
                try {
                    const saved = await updateSettings.mutateAsync({
                        model: value.model.trim()
                    });
                    settingsForm.setFieldValue('model', saved.model);
                    toast.success(saved.is_overridden ? 'Exie model override saved.' : 'Exie is using the deployment-configured model.');
                    return null;
                } catch (error: unknown) {
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }

                    return {
                        form: 'Failed to update the Exie model.'
                    };
                }
            }
        }
    }));

    $effect(() => {
        if (!settings || loadedAvailabilityKey === availabilityKey) {
            return;
        }

        loadedAvailabilityKey = availabilityKey;
        assistantEnabled = settings.enabled;
    });

    $effect(() => {
        if (!settings || loadedSettingsKey === settingsKey) {
            return;
        }

        loadedSettingsKey = settingsKey;
        settingsForm.setFieldValue('model', settings.model);
    });

    async function resetModel() {
        try {
            const saved = await updateSettings.mutateAsync({
                model: null
            });
            settingsForm.setFieldValue('model', saved.model);
            toast.success('Exie model reset to the deployment default.');
        } catch {
            toast.error('Failed to reset the Exie model.');
        }
    }

    async function saveAvailability() {
        try {
            const saved = await updateEnabledSettings.mutateAsync({
                enabled: assistantEnabled
            });
            assistantEnabled = saved.enabled;
            toast.success(saved.enabled ? 'Exie is enabled.' : 'Exie is disabled.');
        } catch {
            toast.error('Failed to update Exie availability.');
        }
    }

    async function resetAvailability() {
        try {
            const saved = await updateEnabledSettings.mutateAsync({
                enabled: null
            });
            assistantEnabled = saved.enabled;
            toast.success('Exie availability reset to the deployment default.');
        } catch {
            toast.error('Failed to reset Exie availability.');
        }
    }

    const statCards = $derived([
        {
            icon: Building2,
            label: 'Active Organizations',
            value: usage?.active_organizations,
            valueType: 'number'
        },
        {
            icon: MessagesSquare,
            label: 'Turns',
            value: usage?.turns,
            valueType: 'number'
        },
        {
            icon: Bot,
            label: 'Tokens',
            value: totalTokens,
            valueType: 'compact'
        },
        {
            icon: Coins,
            label: 'Total Provider Cost',
            sub: 'across all organizations',
            value: usage?.cost_usd,
            valueType: 'currency'
        },
        {
            icon: Gauge,
            label: 'Tokens per Turn',
            value: tokensPerTurn,
            valueType: 'number'
        },
        {
            icon: Wrench,
            label: 'Average Cost',
            sub: 'per active organization',
            value: averageCost,
            valueType: 'currency'
        }
    ]);
</script>

<div class="flex flex-col gap-6">
    <Card.Root>
        <Card.Header>
            <div class="flex flex-wrap items-center gap-2">
                <Card.Title>Availability</Card.Title>
                {#if settings}
                    <Badge variant={settings.is_enabled_overridden ? 'secondary' : 'outline'}>
                        {settings.is_enabled_overridden ? 'Runtime override' : 'Deployment default'}
                    </Badge>
                {/if}
            </div>
            <Card.Description>Enable or disable Exie for all organizations without restarting the app.</Card.Description>
        </Card.Header>
        {#if settingsQuery.isPending}
            <Card.Content class="flex items-center gap-2">
                <Spinner />
                <Muted>Loading Exie availability...</Muted>
            </Card.Content>
        {:else if settingsQuery.isError}
            <Card.Content>
                <p class="text-destructive text-sm">Failed to load Exie availability.</p>
            </Card.Content>
        {:else}
            <Card.Content class="space-y-2">
                <div class="flex items-center justify-between gap-6 rounded-lg border p-4">
                    <div class="space-y-1">
                        <label class="text-sm font-medium" for="assistant-enabled">Exie enabled</label>
                        <Muted class="text-xs"
                            >Disabled Exie requests are rejected immediately. The deployment default is {settings?.configured_enabled
                                ? 'enabled'
                                : 'disabled'}.</Muted
                        >
                    </div>
                    <Switch id="assistant-enabled" bind:checked={assistantEnabled} disabled={updateEnabledSettings.isPending} />
                </div>
                {#if settings && !settings.is_configured}
                    <p class="text-destructive text-sm">An OpenRouter API key must be configured before Exie can be used.</p>
                {/if}
            </Card.Content>
            <Card.Footer class="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
                {#if settings?.is_enabled_overridden}
                    <Button type="button" variant="outline" disabled={updateEnabledSettings.isPending} onclick={resetAvailability}>
                        <RotateCcw data-icon="inline-start" />
                        Reset to Deployment Default
                    </Button>
                {/if}
                <Button type="button" disabled={updateEnabledSettings.isPending || assistantEnabled === settings?.enabled} onclick={saveAvailability}>
                    {#if updateEnabledSettings.isPending}
                        <Spinner data-icon="inline-start" />
                        Saving...
                    {:else}
                        <Save data-icon="inline-start" />
                        Save Availability
                    {/if}
                </Button>
            </Card.Footer>
        {/if}
    </Card.Root>

    <Card.Root>
        <Card.Header>
            <div class="flex flex-wrap items-center gap-2">
                <Card.Title>Model Configuration</Card.Title>
                {#if settings}
                    <Badge variant={settings.is_overridden ? 'secondary' : 'outline'}>
                        {settings.is_overridden ? 'Runtime override' : 'Deployment default'}
                    </Badge>
                {/if}
            </div>
            <Card.Description>Choose the OpenRouter model used for new Exie conversations and turns. Changes apply without restarting the app.</Card.Description
            >
        </Card.Header>
        {#if settingsQuery.isPending}
            <Card.Content class="flex items-center gap-2">
                <Spinner />
                <Muted>Loading model configuration...</Muted>
            </Card.Content>
        {:else if settingsQuery.isError}
            <Card.Content>
                <p class="text-destructive text-sm">Failed to load the Exie model configuration.</p>
            </Card.Content>
        {:else}
            <form
                onsubmit={(event) => {
                    event.preventDefault();
                    void settingsForm.handleSubmit();
                }}
            >
                <Card.Content>
                    <Field.FieldGroup>
                        <settingsForm.Field name="model">
                            {#snippet children(field)}
                                <Field.Field data-invalid={ariaInvalid(field)}>
                                    <Field.Label for={field.name}>OpenRouter model ID</Field.Label>
                                    <Input
                                        id={field.name}
                                        value={field.state.value}
                                        onblur={field.handleBlur}
                                        oninput={(event) => field.handleChange(event.currentTarget.value)}
                                        aria-invalid={ariaInvalid(field)}
                                        autocomplete="off"
                                        placeholder="z-ai/glm-5.3-flash"
                                    />
                                    <Field.Description>
                                        Enter a model slug such as
                                        <a href="https://openrouter.ai/z-ai/glm-5.3-flash" target="_blank" rel="noopener noreferrer">z-ai/glm-5.3-flash</a>. The
                                        deployment default is <code>{settings?.configured_model}</code>.
                                    </Field.Description>
                                    <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                                </Field.Field>
                            {/snippet}
                        </settingsForm.Field>
                        <settingsForm.Subscribe selector={(state) => state.errors}>
                            {#snippet children(errors)}
                                <ErrorMessage message={getFormErrorMessages(errors)} />
                            {/snippet}
                        </settingsForm.Subscribe>
                    </Field.FieldGroup>
                </Card.Content>
                <Card.Footer class="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
                    {#if settings?.is_overridden}
                        <Button type="button" variant="outline" disabled={updateSettings.isPending} onclick={resetModel}>
                            <RotateCcw data-icon="inline-start" />
                            Reset to Deployment Default
                        </Button>
                    {/if}
                    <settingsForm.Subscribe selector={(state) => state.isSubmitting}>
                        {#snippet children(isSubmitting)}
                            <Button type="submit" disabled={isSubmitting || updateSettings.isPending}>
                                {#if isSubmitting || updateSettings.isPending}
                                    <Spinner data-icon="inline-start" />
                                    Saving...
                                {:else}
                                    <Save data-icon="inline-start" />
                                    Save Model
                                {/if}
                            </Button>
                        {/snippet}
                    </settingsForm.Subscribe>
                </Card.Footer>
            </form>
        {/if}
    </Card.Root>

    <div class="flex flex-wrap items-end justify-between gap-4">
        <Muted>Monthly Exie usage, provider cost, and plan-limit health across all organizations</Muted>
        <label class="flex flex-col gap-1 text-sm font-medium">
            Month
            <Input class="w-40" type="month" max={currentMonth} bind:value={selectedMonth} />
        </label>
    </div>

    {#if usageQuery.isError}
        <Card.Root>
            <Card.Content class="pt-6">
                <p class="text-destructive text-sm">Failed to load Exie usage. Please try again.</p>
            </Card.Content>
        </Card.Root>
    {:else}
        <div class="grid grid-cols-2 gap-4 sm:grid-cols-3">
            {#each statCards as card (card.label)}
                {@const Icon = card.icon}
                <Card.Root class="flex flex-col justify-between">
                    <Card.Header class="flex flex-row items-center justify-between space-y-0 pb-2">
                        <Card.Title class="text-sm font-medium">{card.label}</Card.Title>
                        <Icon class="text-muted-foreground size-4" aria-hidden="true" />
                    </Card.Header>
                    <Card.Content>
                        {#if usageQuery.isPending}
                            <Skeleton class="h-8 w-24 rounded" />
                        {:else}
                            <div class="text-2xl font-bold">
                                {#if card.valueType === 'currency'}
                                    <Currency value={card.value ?? null} />
                                {:else if card.valueType === 'compact'}
                                    <NumberCompact value={card.value ?? null} />
                                {:else}
                                    <Number value={card.value ?? null} />
                                {/if}
                            </div>
                            {#if card.sub}
                                <p class="text-muted-foreground mt-0.5 text-xs">{card.sub}</p>
                            {/if}
                        {/if}
                    </Card.Content>
                </Card.Root>
            {/each}
        </div>

        <Card.Root>
            <Card.Header class="flex flex-row flex-wrap items-start justify-between gap-3">
                <div class="space-y-1">
                    <Card.Title>Organization Usage</Card.Title>
                    <Card.Description>Usage is ordered by provider cost, then token consumption. Durable totals may lag by up to five minutes.</Card.Description
                    >
                </div>
            </Card.Header>
            <Card.Content class="px-0">
                {#if usageQuery.isPending}
                    <div class="space-y-3 px-4 pb-2">
                        {#each [0, 1, 2, 3, 4] as row (row)}
                            <Skeleton class="h-12 w-full rounded" aria-label={`Loading organization ${row + 1}`} />
                        {/each}
                    </div>
                {:else if usage?.organizations.length === 0}
                    <p class="text-muted-foreground px-4 py-10 text-center text-sm">No Exie usage was recorded for this month.</p>
                {:else}
                    <Table.Root>
                        <Table.Header>
                            <Table.Row>
                                <Table.Head class="pl-4">Organization</Table.Head>
                                <Table.Head>Last Used</Table.Head>
                                <Table.Head class="text-right">Activity</Table.Head>
                                <Table.Head class="text-right">Tokens</Table.Head>
                                <Table.Head>Token Limit</Table.Head>
                                <Table.Head class="text-right">Cost</Table.Head>
                                <Table.Head>Cost Limit</Table.Head>
                                <Table.Head>Outcomes</Table.Head>
                                <Table.Head class="pr-4 text-right">Blocked</Table.Head>
                            </Table.Row>
                        </Table.Header>
                        <Table.Body>
                            {#each usage?.organizations ?? [] as organization (organization.organization_id)}
                                {@const blockedCount = getBlockedCount(organization)}
                                {@const tokenRisk = getUsageRisk(organization.token_utilization, organization.blocked_by_token_limit)}
                                {@const costRisk = getUsageRisk(organization.cost_utilization, organization.blocked_by_cost_limit)}
                                <Table.Row>
                                    <Table.Cell class="max-w-64 pl-4 whitespace-normal">
                                        <a
                                            class="text-primary font-medium underline-offset-4 hover:underline"
                                            href={resolve('/(app)/organization/[organizationId]/manage', {
                                                organizationId: organization.organization_id
                                            })}
                                        >
                                            {organization.organization_name}
                                        </a>
                                        <div class="text-muted-foreground mt-0.5 text-xs">{organization.plan_id}</div>
                                    </Table.Cell>
                                    <Table.Cell><TimeAgo value={organization.last_used_utc} /></Table.Cell>
                                    <Table.Cell class="text-right">
                                        <div><Number value={organization.turns} /> turns</div>
                                        <div class="text-muted-foreground mt-0.5 text-xs">
                                            <Number value={organization.provider_requests} /> requests · <Number value={organization.tool_calls} /> tools
                                        </div>
                                    </Table.Cell>
                                    <Table.Cell
                                        class="text-right"
                                        title={`${organization.prompt_tokens.toLocaleString()} prompt, ${organization.completion_tokens.toLocaleString()} completion`}
                                    >
                                        <NumberCompact value={getTotalTokens(organization)} />
                                    </Table.Cell>
                                    <Table.Cell>
                                        <Badge variant={riskBadgeVariant(tokenRisk)}>{utilizationLabel(organization.token_utilization)}</Badge>
                                        {#if organization.monthly_token_limit}
                                            <span class="text-muted-foreground ml-1 text-xs">of <NumberCompact value={organization.monthly_token_limit} /></span
                                            >
                                        {/if}
                                    </Table.Cell>
                                    <Table.Cell class="text-right"><Currency value={organization.cost_usd} /></Table.Cell>
                                    <Table.Cell>
                                        <Badge variant={riskBadgeVariant(costRisk)}>{utilizationLabel(organization.cost_utilization)}</Badge>
                                        {#if organization.monthly_cost_limit_usd}
                                            <span class="text-muted-foreground ml-1 text-xs">of <Currency value={organization.monthly_cost_limit_usd} /></span>
                                        {/if}
                                    </Table.Cell>
                                    <Table.Cell>
                                        <span class="text-emerald-600 dark:text-emerald-400"><Number value={organization.completed} /></span>
                                        <span class="text-muted-foreground"> / </span>
                                        <span class={organization.failed > 0 ? 'text-destructive' : 'text-muted-foreground'}
                                            ><Number value={organization.failed} /></span
                                        >
                                        <span class="text-muted-foreground"> / <Number value={organization.cancelled} /></span>
                                        <span class="sr-only"> completed / failed / cancelled</span>
                                    </Table.Cell>
                                    <Table.Cell class="pr-4 text-right">
                                        {#if blockedCount > 0}
                                            <Badge variant="destructive">{blockedCount}</Badge>
                                        {:else}
                                            <span class="text-muted-foreground">—</span>
                                        {/if}
                                    </Table.Cell>
                                </Table.Row>
                            {/each}
                        </Table.Body>
                    </Table.Root>
                {/if}
            </Card.Content>
        </Card.Root>
    {/if}
</div>
