<script lang="ts">
    import type { NewRateNotificationRule, UpdateRateNotificationRule, ViewRateNotificationRule } from '$features/rate-notifications/types';

    import { A, Muted } from '$comp/typography';
    import * as Alert from '$comp/ui/alert';
    import { Button } from '$comp/ui/button';
    import { Input } from '$comp/ui/input';
    import { Label } from '$comp/ui/label';
    import * as Select from '$comp/ui/select';
    import { Switch } from '$comp/ui/switch';
    import AlertTriangleIcon from '@lucide/svelte/icons/alert-triangle';
    import InfoIcon from '@lucide/svelte/icons/info';
    import { toast } from 'svelte-sonner';

    import { createRateNotificationRule, updateRateNotificationRule } from '$features/rate-notifications/api.svelte';
    import { SIGNAL_LABELS, WINDOW_OPTIONS } from '$features/rate-notifications/types';
    import type { RateNotificationSignal, RateNotificationSubject } from '$features/rate-notifications/types';
    // NOTE: mutations must be created at initialization, not inside event handlers (Svelte/TanStack context requirement)

    interface Props {
        hasPremiumFeatures?: boolean;
        projectId: string | undefined;
        rule?: ViewRateNotificationRule;
        upgrade: () => Promise<void> | void;
        userId: string | undefined;
        onSaved?: (rule: ViewRateNotificationRule) => void;
        onCancel?: () => void;
    }

    let { hasPremiumFeatures = false, projectId, rule, upgrade, userId, onSaved, onCancel }: Props = $props();

    const isEditing = $derived(!!rule);

    // Form state
    let name = $state(rule?.name ?? '');
    let signal = $state<RateNotificationSignal>(rule?.signal ?? 'Errors');
    let subject = $state<RateNotificationSubject>(rule?.subject ?? 'Project');
    let stackId = $state(rule?.stack_id ?? '');
    let threshold = $state(rule?.threshold ?? 10);
    let window = $state(rule?.window ?? '00:05:00');
    let cooldown = $state(rule?.cooldown ?? '00:30:00');
    let isEnabled = $state(rule?.is_enabled ?? true);
    let saving = $state(false);
    let formError = $state<string | undefined>();

    // Validation
    const nameError = $derived(name.trim().length === 0 ? 'Name is required.' : name.length > 100 ? 'Name must be ≤ 100 characters.' : undefined);
    const thresholdError = $derived(threshold < 1 ? 'Threshold must be at least 1.' : undefined);
    const stackIdError = $derived(subject === 'Stack' && !stackId.trim() ? 'Stack ID is required when subject is Stack.' : undefined);

    function parseSeconds(iso: string): number {
        const parts = iso.split(':');
        const h = parseInt(parts[0] ?? '0', 10);
        const m = parseInt(parts[1] ?? '0', 10);
        const s = parseInt(parts[2] ?? '0', 10);
        return h * 3600 + m * 60 + s;
    }
    const cooldownError = $derived(
        parseSeconds(cooldown) < parseSeconds(window) ? 'Cooldown must be at least as long as the window.' : undefined
    );

    const hasErrors = $derived(!!(nameError || thresholdError || stackIdError || cooldownError));

    // When subject changes to Project, clear stackId
    $effect(() => {
        if (subject === 'Project') {
            stackId = '';
        }
    });

    // If non-premium, force isEnabled to false
    $effect(() => {
        if (!hasPremiumFeatures) {
            isEnabled = false;
        }
    });

    const createMutation = createRateNotificationRule({
        route: {
            get projectId() { return projectId; },
            get userId() { return userId; }
        }
    });

    const updateMutation = updateRateNotificationRule({
        route: {
            get projectId() { return rule?.project_id; },
            get ruleId() { return rule?.id; },
            get userId() { return rule?.user_id; }
        }
    });

    async function handleSubmit() {
        if (hasErrors || saving) return;

        saving = true;
        formError = undefined;

        try {
            if (isEditing && rule) {
                const body: UpdateRateNotificationRule = {
                    cooldown,
                    is_enabled: hasPremiumFeatures ? isEnabled : false,
                    name: name.trim(),
                    signal,
                    stack_id: subject === 'Stack' ? stackId.trim() || undefined : undefined,
                    subject,
                    threshold,
                    window
                };
                const updated = await updateMutation.mutateAsync(body);
                toast.success('Rule updated.');
                onSaved?.(updated);
            } else {
                const body: NewRateNotificationRule = {
                    cooldown,
                    is_enabled: hasPremiumFeatures ? isEnabled : false,
                    name: name.trim(),
                    signal,
                    stack_id: subject === 'Stack' ? stackId.trim() || undefined : undefined,
                    subject,
                    threshold,
                    window
                };
                const created = await createMutation.mutateAsync(body);
                toast.success('Rule created.');
                onSaved?.(created);
            }
        } catch (err: unknown) {
            const msg = (err as { detail?: string })?.detail ?? 'Failed to save rule. Please try again.';
            formError = msg;
            toast.error(msg);
        } finally {
            saving = false;
        }
    }
</script>

<form class="space-y-5" onsubmit={(e) => { e.preventDefault(); handleSubmit(); }}>
    {#if !hasPremiumFeatures}
        <Alert.Root variant="information">
            <InfoIcon />
            <Alert.Title>
                <A onclick={upgrade}>Upgrade now</A> to enable and create rate notification rules.
            </Alert.Title>
        </Alert.Root>
    {/if}

    <Alert.Root variant="information">
        <AlertTriangleIcon />
        <Alert.Title>This rule may be noisy. Use a cooldown to avoid repeated emails.</Alert.Title>
    </Alert.Root>

    <!-- Name -->
    <div class="space-y-1.5">
        <Label for="rule-name">Name</Label>
        <Input
            id="rule-name"
            bind:value={name}
            placeholder="e.g. Production error storm"
            maxlength={100}
            disabled={saving}
            aria-invalid={!!nameError}
            aria-describedby={nameError ? 'rule-name-error' : undefined}
        />
        {#if nameError}
            <p id="rule-name-error" class="text-destructive text-xs">{nameError}</p>
        {/if}
    </div>

    <!-- Signal -->
    <div class="space-y-1.5">
        <Label for="rule-signal">Signal</Label>
        <Select.Root type="single" bind:value={signal}>
            <Select.Trigger id="rule-signal" class="w-full" disabled={saving}>
                {SIGNAL_LABELS[signal]}
            </Select.Trigger>
            <Select.Content>
                {#each Object.entries(SIGNAL_LABELS) as [value, label] (value)}
                    <Select.Item value={value as RateNotificationSignal}>{label}</Select.Item>
                {/each}
            </Select.Content>
        </Select.Root>
    </div>

    <!-- Subject -->
    <div class="space-y-1.5">
        <Label for="rule-subject">Subject</Label>
        <Select.Root type="single" bind:value={subject}>
            <Select.Trigger id="rule-subject" class="w-full" disabled={saving}>
                {subject}
            </Select.Trigger>
            <Select.Content>
                <Select.Item value="Project">Project</Select.Item>
                <Select.Item value="Stack">Stack</Select.Item>
            </Select.Content>
        </Select.Root>
    </div>

    <!-- Stack ID (shown only when subject = Stack) -->
    {#if subject === 'Stack'}
        <div class="space-y-1.5">
            <Label for="rule-stack-id">Stack ID</Label>
            <Input
                id="rule-stack-id"
                bind:value={stackId}
                placeholder="Stack ID"
                disabled={saving}
                aria-invalid={!!stackIdError}
                aria-describedby={stackIdError ? 'rule-stack-id-error' : undefined}
            />
            {#if stackIdError}
                <p id="rule-stack-id-error" class="text-destructive text-xs">{stackIdError}</p>
            {/if}
        </div>
    {/if}

    <!-- Threshold -->
    <div class="space-y-1.5">
        <Label for="rule-threshold">Threshold (events)</Label>
        <Input
            id="rule-threshold"
            type="number"
            bind:value={threshold}
            min={1}
            step={1}
            disabled={saving}
            aria-invalid={!!thresholdError}
            aria-describedby={thresholdError ? 'rule-threshold-error' : undefined}
        />
        {#if thresholdError}
            <p id="rule-threshold-error" class="text-destructive text-xs">{thresholdError}</p>
        {/if}
    </div>

    <!-- Window -->
    <div class="space-y-1.5">
        <Label for="rule-window">Window</Label>
        <Select.Root type="single" bind:value={window}>
            <Select.Trigger id="rule-window" class="w-full" disabled={saving}>
                {WINDOW_OPTIONS.find((o) => o.value === window)?.label ?? window}
            </Select.Trigger>
            <Select.Content>
                {#each WINDOW_OPTIONS as option (option.value)}
                    <Select.Item value={option.value}>{option.label}</Select.Item>
                {/each}
            </Select.Content>
        </Select.Root>
    </div>

    <!-- Cooldown -->
    <div class="space-y-1.5">
        <Label for="rule-cooldown">Cooldown</Label>
        <Select.Root type="single" bind:value={cooldown}>
            <Select.Trigger id="rule-cooldown" class="w-full" disabled={saving}>
                {WINDOW_OPTIONS.find((o) => o.value === cooldown)?.label ?? cooldown}
            </Select.Trigger>
            <Select.Content>
                {#each WINDOW_OPTIONS as option (option.value)}
                    <Select.Item value={option.value}>{option.label}</Select.Item>
                {/each}
                <!-- Additional cooldown durations beyond standard windows -->
                <Select.Item value="02:00:00">2 hours</Select.Item>
                <Select.Item value="04:00:00">4 hours</Select.Item>
                <Select.Item value="08:00:00">8 hours</Select.Item>
                <Select.Item value="24:00:00">24 hours</Select.Item>
            </Select.Content>
        </Select.Root>
        {#if cooldownError}
            <p class="text-destructive text-xs">{cooldownError}</p>
        {/if}
        <Muted class="text-xs">Further notifications for this rule are suppressed during the cooldown period.</Muted>
    </div>

    <!-- Enabled toggle -->
    <div class="flex items-center gap-3">
        <Switch
            id="rule-enabled"
            bind:checked={isEnabled}
            disabled={saving || !hasPremiumFeatures}
            aria-label="Enable rule"
        />
        <Label for="rule-enabled" class="cursor-pointer">
            {isEnabled ? 'Enabled' : 'Disabled'}
        </Label>
    </div>

    {#if formError}
        <p class="text-destructive text-sm">{formError}</p>
    {/if}

    <div class="flex justify-end gap-2 pt-2">
        {#if onCancel}
            <Button variant="outline" onclick={onCancel} disabled={saving} type="button">Cancel</Button>
        {/if}
        <Button type="submit" disabled={hasErrors || saving || !hasPremiumFeatures}>
            {#if saving}Saving…{:else if isEditing}Save changes{:else}Create rule{/if}
        </Button>
    </div>
</form>
