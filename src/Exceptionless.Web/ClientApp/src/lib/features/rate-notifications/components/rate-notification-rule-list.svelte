<script lang="ts">
    import type { ViewRateNotificationRule } from '$features/rate-notifications/types';

    import ErrorMessage from '$comp/error-message.svelte';
    import { A, Muted } from '$comp/typography';
    import * as Alert from '$comp/ui/alert';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { Badge } from '$comp/ui/badge';
    import { Button, buttonVariants } from '$comp/ui/button';
    import { Skeleton } from '$comp/ui/skeleton';
    import { Switch } from '$comp/ui/switch';
    import {
        deleteRateNotificationRule,
        getRateNotificationRulesQuery,
        postSnoozeRateNotificationRule,
        postUnsnoozeRateNotificationRule,
        putRateNotificationRule
    } from '$features/rate-notifications/api.svelte';
    import { MAX_RULES_PER_PROJECT, RateNotificationSubject, SIGNAL_LABELS, WINDOW_OPTIONS } from '$features/rate-notifications/types';
    import BellOffIcon from '@lucide/svelte/icons/bell-off';
    import InfoIcon from '@lucide/svelte/icons/info';
    import PlusIcon from '@lucide/svelte/icons/plus';
    import Trash2Icon from '@lucide/svelte/icons/trash-2';
    import { toast } from 'svelte-sonner';

    interface Props {
        hasPremiumFeatures?: boolean;
        onCreateClick?: () => void;
        onEditClick?: (rule: ViewRateNotificationRule) => void;
        projectId: string | undefined;
        upgrade: () => Promise<void> | void;
        userId: string | undefined;
    }

    let { hasPremiumFeatures = false, onCreateClick, onEditClick, projectId, upgrade, userId }: Props = $props();

    const route = {
        get projectId() {
            return projectId;
        },
        get userId() {
            return userId;
        }
    };
    const deleteMutation = deleteRateNotificationRule();
    const listQuery = getRateNotificationRulesQuery({ route });
    const snoozeMutation = postSnoozeRateNotificationRule();
    const unsnoozeMutation = postUnsnoozeRateNotificationRule();
    const updateMutation = putRateNotificationRule();

    const rules = $derived(listQuery.data?.data ?? []);
    let confirmDeleteRuleId = $state<string | undefined>();
    let errorMessage = $state<string | undefined>();

    $effect(() => {
        void projectId;
        void userId;
        confirmDeleteRuleId = undefined;
        errorMessage = undefined;
    });

    async function confirmDelete() {
        if (!confirmDeleteRuleId || !projectId || !userId) {
            return;
        }

        errorMessage = undefined;
        try {
            await deleteMutation.mutateAsync({ projectId, ruleId: confirmDeleteRuleId, userId });
            confirmDeleteRuleId = undefined;
        } catch {
            errorMessage = 'Failed to delete rule. Please try again.';
            toast.error(errorMessage);
        }
    }

    async function handleSnooze(rule: ViewRateNotificationRule) {
        if (!projectId || !userId) {
            return;
        }

        errorMessage = undefined;
        try {
            if (rule.is_snoozed) {
                await unsnoozeMutation.mutateAsync({ projectId, ruleId: rule.id, userId });
                toast.success('Rule resumed.');
            } else {
                await snoozeMutation.mutateAsync({ body: { duration_seconds: 3600 }, projectId, ruleId: rule.id, userId });
                toast.success('Rule snoozed for 1 hour.');
            }
        } catch {
            errorMessage = 'Failed to update snooze. Please try again.';
            toast.error(errorMessage);
        }
    }

    async function toggleEnabled(rule: ViewRateNotificationRule, enabled: boolean) {
        if (!projectId || !userId) {
            return;
        }

        errorMessage = undefined;
        try {
            await updateMutation.mutateAsync({ body: { is_enabled: enabled }, projectId, ruleId: rule.id, userId });
        } catch {
            errorMessage = 'Failed to update rule. Please try again.';
            toast.error(errorMessage);
        }
    }

    function windowLabel(value: string): string {
        return WINDOW_OPTIONS.find((option) => option.value === value)?.label ?? value;
    }
</script>

<div class="flex flex-col gap-4">
    <ErrorMessage message={errorMessage} />

    {#if !hasPremiumFeatures}
        <Alert.Root variant="information">
            <InfoIcon aria-hidden="true" />
            <Alert.Title><A onclick={upgrade}>Upgrade now</A> to enable personal rate notifications.</Alert.Title>
        </Alert.Root>
    {/if}

    {#if listQuery.isLoading}
        <div class="flex flex-col gap-2" aria-label="Loading rate notification rules">
            {#each [0, 1] as index (index)}
                <Skeleton class="h-16 rounded-lg" />
            {/each}
        </div>
    {:else if listQuery.isError}
        <ErrorMessage message="Failed to load rate notification rules." />
    {:else if rules.length === 0}
        <div class="flex flex-col items-center gap-3 rounded-lg border border-dashed p-8 text-center">
            <BellOffIcon class="text-muted-foreground size-8" aria-hidden="true" />
            <Muted class="text-sm">No rate notification rules yet.</Muted>
            {#if hasPremiumFeatures}
                <Button variant="outline" size="sm" onclick={onCreateClick}>
                    <PlusIcon data-icon="inline-start" aria-hidden="true" />
                    Create your first rule
                </Button>
            {/if}
        </div>
    {:else}
        <div class="flex flex-col gap-2">
            {#each rules as rule (rule.id)}
                <div class="rounded-lg border p-4">
                    <div class="flex items-start justify-between gap-3">
                        <div class="min-w-0 flex-1">
                            <Button variant="link" disabled={!hasPremiumFeatures} onclick={() => onEditClick?.(rule)}>{rule.name}</Button>
                            <div class="mt-1 flex flex-wrap items-center gap-2">
                                <Badge variant="secondary">{SIGNAL_LABELS[rule.signal]}</Badge>
                                <Muted class="text-xs">≥{rule.threshold} in {windowLabel(rule.window)}</Muted>
                                {#if rule.is_snoozed}
                                    <Badge variant="outline"><BellOffIcon aria-hidden="true" />Snoozed</Badge>
                                {/if}
                                {#if rule.subject === RateNotificationSubject.Stack && rule.stack_id}
                                    <Muted class="text-xs">Stack-scoped</Muted>
                                {/if}
                            </div>
                        </div>
                        <div class="flex shrink-0 items-center gap-2">
                            <Button
                                variant="ghost"
                                size="icon"
                                aria-label={rule.is_snoozed ? 'Resume rule' : 'Snooze rule for 1 hour'}
                                title={rule.is_snoozed ? 'Resume rule' : 'Snooze for 1 hour'}
                                disabled={snoozeMutation.isPending || unsnoozeMutation.isPending}
                                onclick={() => handleSnooze(rule)}
                            >
                                <BellOffIcon aria-hidden="true" />
                            </Button>
                            <Switch
                                id={`rule-enabled-${rule.id}`}
                                checked={rule.is_enabled}
                                disabled={!hasPremiumFeatures || updateMutation.isPending}
                                onCheckedChange={(checked) => toggleEnabled(rule, checked)}
                                aria-label={rule.is_enabled ? 'Disable rule' : 'Enable rule'}
                            />
                            <Button
                                variant="ghost"
                                size="icon"
                                aria-label="Delete rule"
                                disabled={deleteMutation.isPending}
                                onclick={() => (confirmDeleteRuleId = rule.id)}
                            >
                                <Trash2Icon aria-hidden="true" />
                            </Button>
                        </div>
                    </div>
                </div>
            {/each}
        </div>

        {#if hasPremiumFeatures && rules.length < MAX_RULES_PER_PROJECT}
            <Button variant="outline" size="sm" onclick={onCreateClick}>
                <PlusIcon data-icon="inline-start" aria-hidden="true" />
                Add rule
            </Button>
        {/if}
    {/if}
</div>

<AlertDialog.Root open={!!confirmDeleteRuleId} onOpenChange={(open) => !open && (confirmDeleteRuleId = undefined)}>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Delete rule?</AlertDialog.Title>
            <AlertDialog.Description>This action cannot be undone. The rate notification rule will be permanently deleted.</AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action class={buttonVariants({ variant: 'destructive' })} onclick={confirmDelete}>Delete</AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
