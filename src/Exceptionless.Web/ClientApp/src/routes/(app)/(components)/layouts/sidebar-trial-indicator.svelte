<script lang="ts">
    import Hourglass from '@lucide/svelte/icons/hourglass';

    import type { ViewOrganization } from '#features/organizations/models.ts';

    import TimeAgo from '#comp/formatters/time-ago.svelte';
    import * as Sidebar from '#comp/ui/sidebar/index.ts';
    import { TRIAL_PLAN_ID } from '#features/billing/constants.ts';
    import { showUpgradeDialog } from '#features/billing/upgrade-required.svelte.ts';

    interface Props {
        organization: undefined | ViewOrganization;
    }

    let { organization }: Props = $props();

    // The trial expiration job moves the organization to Free when the trial ends, which hides this indicator.
    const trialEndsUtc = $derived(organization?.plan_id === TRIAL_PLAN_ID ? organization.trial_ends_utc : undefined);
    const tooltip = $derived(
        trialEndsUtc
            ? `Trial ends ${new Date(trialEndsUtc).toLocaleDateString(undefined, {
                  dateStyle: 'medium'
              })}`
            : undefined
    );
</script>

{#snippet trialEnds(value: string)}
    Trial ends <TimeAgo {value} />
{/snippet}

{#if organization?.id && trialEndsUtc}
    {@const organizationId = organization.id}
    {@const endsUtc = trialEndsUtc}
    <Sidebar.MenuItem>
        <Sidebar.MenuButton
            size="sm"
            class="text-muted-foreground"
            tooltipContent={tooltip}
            onclick={() => showUpgradeDialog(organizationId, 'Upgrade to keep Exie, alerts, integrations, and 90-day retention when your trial ends.')}
        >
            <Hourglass aria-hidden="true" />
            <span>{@render trialEnds(endsUtc)}</span>
        </Sidebar.MenuButton>
    </Sidebar.MenuItem>
{/if}
