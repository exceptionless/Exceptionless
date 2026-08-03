<script lang="ts">
    import { Button } from '$comp/ui/button';
    import { showUpgradeDialog } from '$features/billing/upgrade-required.svelte';
    import Bot from '@lucide/svelte/icons/bot';

    interface Props {
        message?: string;
        organizationId?: string;
        upgradeRequired?: boolean;
    }

    let { message, organizationId, upgradeRequired = false }: Props = $props();

    function openUpgradeOptions(): void {
        if (organizationId) {
            showUpgradeDialog(organizationId, message);
        }
    }
</script>

<div class="flex h-full flex-col items-center justify-center gap-5 px-6 text-center">
    <div class="bg-primary/10 text-primary flex size-12 items-center justify-center rounded-xl">
        <Bot aria-hidden="true" class="size-7" />
    </div>
    <div class="max-w-80">
        <h3 class="text-base font-semibold">Bring Exie onto your team</h3>
        <p class="text-muted-foreground mt-1 text-sm">{message ?? 'Exie is not available for this organization.'}</p>
        {#if upgradeRequired}
            <p class="text-muted-foreground mt-2 text-sm">Upgrade this organization to investigate errors and manage stacks with Exie.</p>
        {/if}
    </div>
    {#if upgradeRequired && organizationId}
        <Button onclick={openUpgradeOptions}>View upgrade options</Button>
    {/if}
</div>
