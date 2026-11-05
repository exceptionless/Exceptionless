<script lang="ts">
    import Hourglass from '@lucide/svelte/icons/hourglass';

    import type { ViewOrganization } from '#features/organizations/models.ts';

    import * as Sidebar from '#comp/ui/sidebar/index.ts';
    import { TRIAL_PLAN_ID } from '#features/billing/constants.ts';
    import { getTrialDaysRemaining } from '#features/billing/trial.ts';
    import { showUpgradeDialog } from '#features/billing/upgrade-required.svelte.ts';

    interface Props {
        organization: undefined | ViewOrganization;
    }

    let { organization }: Props = $props();

    const daysRemaining = $derived(organization?.plan_id === TRIAL_PLAN_ID ? getTrialDaysRemaining(organization.trial_ends_utc) : 0);
    const label = $derived(daysRemaining === 1 ? '1 day left in trial' : `${daysRemaining} days left in trial`);
</script>

{#if organization?.id && daysRemaining > 0}
    {@const organizationId = organization.id}
    <Sidebar.MenuItem>
        <Sidebar.MenuButton
            size="sm"
            class="text-muted-foreground"
            tooltipContent={label}
            onclick={() => showUpgradeDialog(organizationId, 'Upgrade to keep Exie, alerts, integrations, and 90-day retention when your trial ends.')}
        >
            <Hourglass aria-hidden="true" />
            <span>{label}</span>
        </Sidebar.MenuButton>
    </Sidebar.MenuItem>
{/if}
