<script lang="ts" module>
    // Remounting on another page can beat the user refetch that carries the finished state; celebrate once per session.
    const celebratedUserIds: Record<string, true> = {};
</script>

<script lang="ts">
    import { resolve } from '$app/paths';
    import ChevronDown from '@lucide/svelte/icons/chevron-down';
    import Circle from '@lucide/svelte/icons/circle';
    import CircleCheck from '@lucide/svelte/icons/circle-check';
    import X from '@lucide/svelte/icons/x';
    import { toast } from 'svelte-sonner';
    import { SvelteSet } from 'svelte/reactivity';

    import ExieIcon from '#comp/icons/ExieIcon.svelte';
    import { Badge } from '#comp/ui/badge/index.ts';
    import { Button } from '#comp/ui/button/index.ts';
    import * as Card from '#comp/ui/card/index.ts';
    import * as Collapsible from '#comp/ui/collapsible/index.ts';
    import { Spinner } from '#comp/ui/spinner/index.ts';
    import { getAssistantAccessQuery } from '#features/assistant/api.svelte.ts';
    import { tryUseAssistantControls } from '#features/assistant/controls.svelte.ts';
    import { FREE_PLAN_ID, TRIAL_PLAN_ID } from '#features/billing/index.ts';
    import { getOrganizationQuery } from '#features/organizations/api.svelte.ts';
    import { organization } from '#features/organizations/context.svelte.ts';
    import { getMeQuery, putCurrentUserProductTour, resendVerificationEmail } from '#features/users/api.svelte.ts';
    import { cn } from '#lib/utils.ts';

    import {
        CHECKLIST_DISMISSED_KEY,
        CHECKLIST_VISIT_KEYS,
        type ChecklistItem,
        type ChecklistItemId,
        type ChecklistVisitItemId,
        getChecklistItems,
        isChecklistComplete,
        shouldShowChecklist
    } from '../checklist';
    import { ChecklistLayoutPreference } from '../checklist-layout.svelte';
    import { useOrganizationOnboarding } from '../onboarding-state.svelte';
    import Confetti from './confetti.svelte';
    import ExieGuide from './exie-guide.svelte';

    const meQuery = getMeQuery();
    const organizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return organization.current;
            }
        }
    });
    const onboarding = useOrganizationOnboarding(() => organization.current);
    const assistant = tryUseAssistantControls();
    const assistantAccessQuery = getAssistantAccessQuery({
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });
    const assistantAccess = $derived(assistantAccessQuery.data);
    // Offer Exie where the organization has it, or will once the email is verified (trials); skip it where it needs an upgrade.
    const includeAskExie = $derived(!!assistantAccess?.enabled && (assistantAccess.has_access || !!assistantAccess.email_verification_required));
    const canAskExie = $derived(!!assistantAccess?.has_access && onboarding.hasReceivedEvents && !!assistant?.enabled());
    const recordMutation = putCurrentUserProductTour();
    const resendMutation = resendVerificationEmail({
        route: {
            get id() {
                return meQuery.data?.id;
            }
        }
    });

    // Full list until the first event; a single row afterwards unless this user chose otherwise.
    const layoutPreference = new ChecklistLayoutPreference(() => meQuery.data?.id);
    const isExpanded = $derived(layoutPreference.expanded ?? !onboarding.hasReceivedEvents);
    // Visits recorded in this session show as done before the user refetch finishes.
    const visitedItems = new SvelteSet<ChecklistVisitItemId>();
    let dismissed = $state(false);
    let celebrating = $state(false);

    const items = $derived(
        getChecklistItems({
            emailVerified: !!meQuery.data?.is_email_address_verified,
            hasEvents: onboarding.hasReceivedEvents,
            hasProject: onboarding.hasProject,
            includeAskExie,
            productTours: meQuery.data?.product_tours
        }).map((item) =>
            item.kind === 'visit' && visitedItems.has(item.id as ChecklistVisitItemId)
                ? {
                      ...item,
                      complete: true
                  }
                : item
        )
    );
    const completedCount = $derived(items.filter((item) => item.complete).length);
    const nextItem = $derived(items.find((item) => !item.complete));
    const isComplete = $derived(isChecklistComplete(items));
    const isVisible = $derived(
        !dismissed &&
            !!meQuery.data &&
            onboarding.isReady &&
            // Wait for Exie's access so the item count doesn't jump (or finish the list early) when it loads.
            !assistantAccessQuery.isPending &&
            shouldShowChecklist({
                organizationCreatedUtc: organizationQuery.data?.created_utc,
                productTours: meQuery.data.product_tours
            })
    );
    const isFreePlan = $derived(organizationQuery.data?.plan_id === FREE_PLAN_ID);
    const isTrialPlan = $derived(organizationQuery.data?.plan_id === TRIAL_PLAN_ID);

    // Finishing the list retires it for good: celebrate once, then record it like a dismissal.
    $effect(() => {
        const userId = meQuery.data?.id;
        if (!isVisible || !isComplete || !userId || celebratedUserIds[userId]) {
            return;
        }

        celebratedUserIds[userId] = true;
        celebrating = true;
        toast.success("You finished Exie's checklist!", {
            description: "That's everything. I'll keep watching for new errors."
        });
        void hide(userId).catch(() => {
            // Leave the finished card up so the user can hide it with the button.
            dismissed = false;
        });
    });

    interface ItemCopy {
        action?: () => void;
        description?: string;
        external?: boolean;
        href?: string;
        label: string;
    }

    async function dismiss(): Promise<void> {
        const userId = meQuery.data?.id;
        if (!userId) {
            return;
        }

        try {
            await hide(userId);
        } catch {
            dismissed = false;
            toast.error('We could not hide the checklist. Please try again.');
        }
    }

    function getItemCopy(id: ChecklistItemId): ItemCopy {
        switch (id) {
            case 'account':
                return {
                    label: 'Create your account'
                };
            case 'ai-tools':
                return {
                    description: 'Ask about errors from your editor or chat. Works on every plan.',
                    href: resolve('/(app)/account/ai-tools'),
                    label: 'Connect your AI assistant via MCP'
                };
            case 'ask-exie':
                return {
                    action: canAskExie ? () => assistant?.ask('Find my most recent error and explain what went wrong and how to fix it.') : undefined,
                    description: assistantAccess?.has_access
                        ? onboarding.hasReceivedEvents
                            ? 'Exie explains what went wrong and how to fix it.'
                            : 'Available once your first event arrives.'
                        : 'Verify your email to unlock Exie.',
                    label: 'Ask Exie about your first error'
                };
            case 'first-event':
                return {
                    href: onboarding.continueSetupHref,
                    label: 'Catch your first event'
                };
            case 'invite-team':
                return {
                    description: isFreePlan ? 'Inviting teammates is a paid-plan feature.' : 'Triage together and share the load.',
                    href: organization.current
                        ? resolve('/(app)/organization/[organizationId]/users', {
                              organizationId: organization.current
                          })
                        : undefined,
                    label: 'Invite your team'
                };
            case 'notifications':
                return {
                    description: 'New errors and a daily summary are on by default. Pick what you want.',
                    href: resolve('/(app)/account/notifications'),
                    label: 'Review your notifications'
                };
            case 'project':
                return {
                    href: resolve('/(app)/project/add'),
                    label: 'Name your project'
                };
            case 'verify-email':
                return {
                    // Trials hold Exie and team invites until the email is verified, so say what verifying unlocks.
                    description:
                        [
                            meQuery.data?.email_address ? `Check ${meQuery.data.email_address} for the link.` : undefined,
                            isTrialPlan ? 'It unlocks Exie and team invites during your trial.' : undefined
                        ]
                            .filter(Boolean)
                            .join(' ') || undefined,
                    label: 'Verify your email'
                };
            case 'versions':
                return {
                    description: 'Tag events with your app version and environment.',
                    external: true,
                    href: 'https://exceptionless.com/docs/versioning/',
                    label: 'Track versions & environments'
                };
        }
    }

    /** Hides the checklist now and records it so it stays hidden on every page and device. */
    async function hide(userId: string): Promise<void> {
        dismissed = true;
        await recordMutation.mutateAsync({
            tourName: CHECKLIST_DISMISSED_KEY,
            userId
        });
        // The checklist replaces the generic welcome card; don't show that afterwards.
        void recordMutation
            .mutateAsync({
                tourName: 'app-welcome',
                userId
            })
            .catch(() => undefined);
    }

    function recordVisit(item: ChecklistItem): void {
        const userId = meQuery.data?.id;
        if (item.kind !== 'visit' || item.complete || !userId) {
            return;
        }

        const id = item.id as ChecklistVisitItemId;
        // Mark it done after the click is handled: re-rendering now would swap the link's href (or the link
        // itself) before the router reads it, sending the user to the next item instead.
        setTimeout(() => visitedItems.add(id));
        void recordMutation
            .mutateAsync({
                tourName: CHECKLIST_VISIT_KEYS[id],
                userId
            })
            .catch(() => visitedItems.delete(id));
    }

    async function resend(): Promise<void> {
        try {
            await resendMutation.mutateAsync();
            toast.success('Verification email sent', {
                description: 'Check your inbox for the link.'
            });
        } catch {
            toast.error('We could not send the verification email. Please try again.');
        }
    }
</script>

{#if celebrating}
    <Confetti />
{/if}

{#if isVisible}
    <Card.Root aria-labelledby="getting-started-checklist-title" data-getting-started-checklist role="region" size="sm">
        <Collapsible.Root onOpenChange={(open) => layoutPreference.set(open)} open={isExpanded}>
            <Card.Header class="flex flex-row items-center gap-3">
                <!-- The sparkle inherits brand green; the robot outline uses the foreground color. -->
                <span class="bg-muted text-primary flex size-8 shrink-0 items-center justify-center rounded-lg">
                    <ExieIcon class="[&_g]:text-foreground" height={20} width={20} />
                </span>
                <div class="flex min-w-0 flex-1 flex-col gap-1 md:flex-row md:items-center md:gap-4">
                    <div class="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1">
                        <Card.Title class="shrink-0" id="getting-started-checklist-title">Exie's checklist</Card.Title>
                        <div class="flex items-center gap-2">
                            <div
                                aria-label="Checklist progress"
                                aria-valuemax={items.length}
                                aria-valuemin={0}
                                aria-valuenow={completedCount}
                                class="bg-muted h-1.5 w-20 overflow-hidden rounded-full sm:w-32"
                                role="progressbar"
                            >
                                <div
                                    class="bg-primary h-full rounded-full transition-[width] duration-500"
                                    style:width="{(completedCount / items.length) * 100}%"
                                ></div>
                            </div>
                            <span class="text-muted-foreground text-xs whitespace-nowrap tabular-nums">{completedCount} of {items.length} done</span>
                        </div>
                    </div>
                    {#if !isExpanded}
                        <p class="text-muted-foreground min-w-0 truncate text-sm" data-checklist-next>
                            {#if nextItem}
                                {@const nextCopy = getItemCopy(nextItem.id)}
                                Next:
                                {#if nextCopy.href}
                                    <a
                                        class="text-foreground font-medium underline-offset-4 hover:underline"
                                        href={nextCopy.href}
                                        onclick={() => recordVisit(nextItem)}
                                        rel={nextCopy.external ? 'noopener noreferrer' : undefined}
                                        target={nextCopy.external ? '_blank' : undefined}>{nextCopy.label}</a
                                    >
                                {:else if nextCopy.action}
                                    {@const action = nextCopy.action}
                                    <button
                                        class="text-foreground font-medium underline-offset-4 hover:underline"
                                        onclick={() => {
                                            recordVisit(nextItem);
                                            action();
                                        }}
                                        type="button">{nextCopy.label}</button
                                    >
                                {:else}
                                    <button
                                        class="text-foreground font-medium underline-offset-4 hover:underline"
                                        onclick={() => layoutPreference.set(true)}
                                        type="button">{nextCopy.label}</button
                                    >
                                {/if}
                            {:else}
                                You're all set!
                            {/if}
                        </p>
                    {/if}
                </div>
                <Collapsible.Trigger>
                    {#snippet child({ props })}
                        <Button {...props} aria-label={isExpanded ? 'Collapse checklist' : 'Expand checklist'} size="icon-sm" variant="ghost">
                            <ChevronDown aria-hidden="true" class={cn('transition-transform', isExpanded && 'rotate-180')} />
                        </Button>
                    {/snippet}
                </Collapsible.Trigger>
                <Button aria-label="Dismiss checklist" onclick={dismiss} size="icon-sm" title="Dismiss checklist" variant="ghost">
                    <X aria-hidden="true" />
                </Button>
            </Card.Header>
            <Collapsible.Content>
                <Card.Content class="pt-3">
                    {#if isComplete}
                        <ExieGuide mood="celebrate" size={56} title="You're all set!">
                            You've done everything on my list. I'll keep watching for new errors.
                            {#snippet actions()}
                                <Button onclick={dismiss} size="sm">Hide checklist</Button>
                            {/snippet}
                        </ExieGuide>
                    {:else}
                        <ul class="grid gap-1 md:grid-cols-2">
                            {#each items as item (item.id)}
                                {@const copy = getItemCopy(item.id)}
                                <li>
                                    {#snippet row()}
                                        {#if item.complete}
                                            <CircleCheck aria-hidden="true" class="text-primary mt-0.5 size-4 shrink-0" />
                                        {:else}
                                            <Circle aria-hidden="true" class="text-muted-foreground mt-0.5 size-4 shrink-0" />
                                        {/if}
                                        <span class="flex min-w-0 flex-col">
                                            <span class={cn('text-sm font-medium', item.complete ? 'text-muted-foreground line-through' : 'text-foreground')}>
                                                {copy.label}<span class="sr-only">{item.complete ? ' (done)' : ' (to do)'}</span>
                                            </span>
                                            {#if copy.description && !item.complete}
                                                <span class="text-muted-foreground text-xs">{copy.description}</span>
                                            {/if}
                                        </span>
                                    {/snippet}

                                    {#if copy.href && !item.complete}
                                        <a
                                            class="hover:bg-muted focus-visible:ring-ring/50 flex items-start gap-2 rounded-md p-2 outline-none focus-visible:ring-3"
                                            href={copy.href}
                                            onclick={() => recordVisit(item)}
                                            rel={copy.external ? 'noopener noreferrer' : undefined}
                                            target={copy.external ? '_blank' : undefined}
                                        >
                                            {@render row()}
                                            {#if item.id === 'invite-team' && isFreePlan}
                                                <Badge class="ml-auto shrink-0" variant="secondary">Paid plans</Badge>
                                            {/if}
                                        </a>
                                    {:else if copy.action && !item.complete}
                                        {@const action = copy.action}
                                        <button
                                            class="hover:bg-muted focus-visible:ring-ring/50 flex w-full items-start gap-2 rounded-md p-2 text-left outline-none focus-visible:ring-3"
                                            onclick={() => {
                                                recordVisit(item);
                                                action();
                                            }}
                                            type="button"
                                        >
                                            {@render row()}
                                        </button>
                                    {:else}
                                        <div class="flex items-start gap-2 p-2">
                                            {@render row()}
                                            {#if item.id === 'verify-email' && !item.complete}
                                                <Button
                                                    class="ml-auto shrink-0"
                                                    disabled={resendMutation.isPending}
                                                    onclick={resend}
                                                    size="xs"
                                                    variant="outline"
                                                >
                                                    {#if resendMutation.isPending}<Spinner data-icon="inline-start" />{/if}
                                                    Resend
                                                </Button>
                                            {/if}
                                        </div>
                                    {/if}
                                </li>
                            {/each}
                        </ul>
                    {/if}
                </Card.Content>
            </Collapsible.Content>
        </Collapsible.Root>
    </Card.Root>
{/if}
