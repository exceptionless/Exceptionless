<script lang="ts">
    import { H3, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Spinner } from '$comp/ui/spinner';
    import { showUpgradeDialog } from '$features/billing/upgrade-required.svelte';
    import Bot from '@lucide/svelte/icons/bot';

    import type { AssistantAccessState } from '../models';

    interface Props {
        accessState?: AssistantAccessState;
        message?: string;
        minimumPlanId?: string;
        onAccessChanged?: () => Promise<void> | void;
        onRetry?: () => Promise<void> | void;
        organizationId?: string;
    }

    let { accessState = 'disabled', message, minimumPlanId, onAccessChanged, onRetry, organizationId }: Props = $props();

    function openUpgradeOptions(): void {
        if (organizationId) {
            showUpgradeDialog(organizationId, message, {
                directToPlanPicker: true,
                initialTierId: minimumPlanId,
                onSuccess: onAccessChanged
            });
        }
    }
</script>

<div class="flex h-full flex-col items-center justify-center gap-5 px-6 text-center">
    <div class="bg-primary/10 text-primary flex size-12 items-center justify-center rounded-xl">
        <Bot aria-hidden="true" class="size-7" />
    </div>
    <div class="max-w-80">
        {#if accessState === 'loading'}
            <H3 class="text-base">Checking Exie access</H3>
            <Muted class="mt-1">Loading this organization’s assistant access…</Muted>
        {:else if accessState === 'error'}
            <H3 class="text-base">Exie couldn’t check access</H3>
            <Muted class="mt-1">{message ?? 'We couldn’t load this organization’s assistant access.'}</Muted>
        {:else}
            <H3 class="text-base">Bring Exie onto your team</H3>
            <Muted class="mt-1">{message ?? 'Exie is not available for this organization.'}</Muted>
        {/if}
        {#if accessState === 'upgrade-required'}
            <Muted class="mt-2">Upgrade this organization to investigate errors and manage stacks with Exie.</Muted>
        {/if}
    </div>
    {#if accessState === 'loading'}
        <Spinner aria-label="Loading Exie access" />
    {:else if accessState === 'error' && onRetry}
        <Button onclick={() => void onRetry()} variant="outline">Retry</Button>
    {:else if accessState === 'upgrade-required' && organizationId}
        <Button onclick={openUpgradeOptions}>Upgrade Plan</Button>
    {/if}
</div>
