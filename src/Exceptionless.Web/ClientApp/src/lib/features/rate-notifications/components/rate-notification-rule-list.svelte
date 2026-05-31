<script lang="ts">
    import type { ViewRateNotificationRule } from '$features/rate-notifications/types';

    import { SIGNAL_LABELS } from '$features/rate-notifications/types';
    import ErrorMessage from '$comp/error-message.svelte';
    import { A, Muted } from '$comp/typography';
    import * as Alert from '$comp/ui/alert';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import { Switch } from '$comp/ui/switch';
    import BellOffIcon from '@lucide/svelte/icons/bell-off';
    import InfoIcon from '@lucide/svelte/icons/info';
    import PlusIcon from '@lucide/svelte/icons/plus';
    import Trash2Icon from '@lucide/svelte/icons/trash-2';
    import { toast } from 'svelte-sonner';

    import {
        deleteRateNotificationRule,
        getRateNotificationRulesQuery,
        snoozeRateNotificationRule,
        unsnoozeRateNotificationRule,
        updateRateNotificationRule
    } from '$features/rate-notifications/api.svelte';
    import { MAX_RULES_PER_PROJECT } from '$features/rate-notifications/types';

    interface Props {
        hasPremiumFeatures?: boolean;
        projectId: string | undefined;
        upgrade: () => Promise<void> | void;
        userId: string | undefined;
        onCreateClick?: () => void;
        onEditClick?: (rule: ViewRateNotificationRule) => void;
    }

    let { hasPremiumFeatures = false, projectId, upgrade, userId, onCreateClick, onEditClick }: Props = $props();

    const listQuery = getRateNotificationRulesQuery({ route: { projectId, userId } });
    const rules = $derived(listQuery.data?.data ?? []);

    const updateMutation = updateRateNotificationRule({ route: { projectId, ruleId: undefined, userId } });
    const deleteMutation = deleteRateNotificationRule({ route: { projectId, ruleId: undefined, userId } });
    const snoozeMutation = snoozeRateNotificationRule({ route: { projectId, ruleId: undefined, userId } });
    const unsnoozeMutation = unsnoozeRateNotificationRule({ route: { projectId, ruleId: undefined, userId } });

    let confirmDeleteRuleId = $state<string | undefined>();
    let errorMessage = $state<string | undefined>();

    async function toggleEnabled(rule: ViewRateNotificationRule, enabled: boolean) {
        errorMessage = undefined;
        try {
            await updateRateNotificationRule({
                route: { projectId: rule.project_id, ruleId: rule.id, userId: rule.user_id }
            }).mutateAsync({ is_enabled: enabled });
        } catch {
            errorMessage = 'Failed to update rule. Please try again.';
            toast.error(errorMessage);
        }
    }

    async function confirmDelete() {
        if (!confirmDeleteRuleId) return;
        errorMessage = undefined;
        try {
            await deleteRateNotificationRule({
                route: { projectId, ruleId: confirmDeleteRuleId, userId }
            }).mutateAsync();
            confirmDeleteRuleId = undefined;
        } catch {
            errorMessage = 'Failed to delete rule. Please try again.';
            toast.error(errorMessage);
        }
    }

    async function handleSnooze(rule: ViewRateNotificationRule) {
        errorMessage = undefined;
        try {
            if (rule.is_snoozed) {
                await unsnoozeRateNotificationRule({
                    route: { projectId: rule.project_id, ruleId: rule.id, userId: rule.user_id }
                }).mutateAsync();
                toast.success('Rule resumed.');
            } else {
                await snoozeRateNotificationRule({
                    route: { projectId: rule.project_id, ruleId: rule.id, userId: rule.user_id }
                }).mutateAsync({ duration_seconds: 3600 });
                toast.success('Rule snoozed for 1 hour.');
            }
        } catch {
            errorMessage = 'Failed to update snooze. Please try again.';
            toast.error(errorMessage);
        }
    }

    function formatWindow(isoWindow: string): string {
        const parts = isoWindow.split(':');
        if (parts.length < 3) return isoWindow;
        const h = parseInt(parts[0] ?? '0', 10);
        const m = parseInt(parts[1] ?? '0', 10);
        if (h > 0) return `${h}h`;
        if (m === 1) return '1 min';
        return `${m} min`;
    }
</script>

<div class="space-y-4">
    <ErrorMessage message={errorMessage} />

    {#if !hasPremiumFeatures}
        <Alert.Root variant="information">
            <InfoIcon />
            <Alert.Title>
                <A onclick={upgrade}>Upgrade now</A> to enable personal rate notifications!
            </Alert.Title>
        </Alert.Root>
    {/if}

    {#if listQuery.isLoading}
        <div class="space-y-2">
            {#each { length: 2 } as _}
                <div class="bg-muted h-16 animate-pulse rounded-lg" />
            {/each}
        </div>
    {:else if rules.length === 0}
        <div class="rounded-lg border border-dashed p-8 text-center">
            <BellOffIcon class="text-muted-foreground mx-auto mb-3 h-8 w-8" />
            <p class="text-muted-foreground text-sm">No rate notification rules yet.</p>
            {#if hasPremiumFeatures}
                <Button class="mt-3" variant="outline" size="sm" onclick={onCreateClick}>
                    <PlusIcon class="mr-1 h-4 w-4" />
                    Create your first rule
                </Button>
            {/if}
        </div>
    {:else}
        <div class="space-y-2">
            {#each rules as rule (rule.id)}
                <div class="rounded-lg border p-4">
                    <div class="flex items-start justify-between gap-3">
                        <div class="min-w-0 flex-1">
                            <button
                                class="text-left text-sm font-medium hover:underline disabled:pointer-events-none"
                                disabled={!hasPremiumFeatures}
                                onclick={() => onEditClick?.(rule)}
                            >
                                {rule.name}
                            </button>
                            <div class="mt-1 flex flex-wrap items-center gap-2">
                                <Badge variant="secondary">{SIGNAL_LABELS[rule.signal]}</Badge>
                                <Muted class="text-xs">
                                    ≥{rule.threshold} in {formatWindow(rule.window)}
                                </Muted>
                                {#if rule.is_snoozed}
                                    <Badge variant="outline" class="text-yellow-600">
                                        <BellOffIcon class="mr-1 h-3 w-3" />
                                        Snoozed
                                    </Badge>
                                {/if}
                                {#if rule.subject === 'Stack' && rule.stack_id}
                                    <Muted class="text-xs">Stack-scoped</Muted>
                                {/if}
                            </div>
                        </div>
                        <div class="flex shrink-0 items-center gap-2">
                            <button
                                class="text-muted-foreground hover:text-foreground"
                                aria-label={rule.is_snoozed ? 'Resume rule' : 'Snooze rule for 1 hour'}
                                title={rule.is_snoozed ? 'Resume rule' : 'Snooze for 1 hour'}
                                onclick={() => handleSnooze(rule)}
                            >
                                <BellOffIcon class="h-4 w-4" />
                            </button>
                            <Switch
                                id={`rule-enabled-${rule.id}`}
                                checked={rule.is_enabled}
                                disabled={!hasPremiumFeatures}
                                onCheckedChange={(checked) => toggleEnabled(rule, checked)}
                                aria-label={rule.is_enabled ? 'Disable rule' : 'Enable rule'}
                            />
                            <button
                                class="text-muted-foreground hover:text-destructive"
                                aria-label="Delete rule"
                                onclick={() => (confirmDeleteRuleId = rule.id)}
                            >
                                <Trash2Icon class="h-4 w-4" />
                            </button>
                        </div>
                    </div>
                </div>
            {/each}
        </div>

        {#if hasPremiumFeatures && rules.length < MAX_RULES_PER_PROJECT}
            <Button variant="outline" size="sm" onclick={onCreateClick}>
                <PlusIcon class="mr-1 h-4 w-4" />
                Add rule
            </Button>
        {/if}
    {/if}
</div>

<!-- Delete confirmation dialog -->
<Dialog.Root open={!!confirmDeleteRuleId} onOpenChange={(open) => !open && (confirmDeleteRuleId = undefined)}>
    <Dialog.Content>
        <Dialog.Header>
            <Dialog.Title>Delete rule?</Dialog.Title>
            <Dialog.Description>
                This action cannot be undone. The rate notification rule will be permanently deleted.
            </Dialog.Description>
        </Dialog.Header>
        <Dialog.Footer>
            <Button variant="outline" onclick={() => (confirmDeleteRuleId = undefined)}>Cancel</Button>
            <Button variant="destructive" onclick={confirmDelete}>Delete</Button>
        </Dialog.Footer>
    </Dialog.Content>
</Dialog.Root>
