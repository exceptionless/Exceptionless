<script lang="ts">
    import { PUBLIC_EXCEPTIONLESS_CLIENT_SETUP_SHOW_SERVER_URL } from '$app/env/public';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import ArrowLeft from '@lucide/svelte/icons/arrow-left';
    import Bot from '@lucide/svelte/icons/bot';
    import Events from '@lucide/svelte/icons/calendar-days';
    import CircleCheck from '@lucide/svelte/icons/circle-check';
    import NotificationSettings from '@lucide/svelte/icons/mail';
    import { useEventListener } from 'runed';
    import { tick } from 'svelte';
    import { toast } from 'svelte-sonner';

    import { Notification, NotificationDescription, NotificationTitle } from '#comp/notification/index.ts';
    import { A, P } from '#comp/typography/index.ts';
    import { Button } from '#comp/ui/button/index.ts';
    import * as Card from '#comp/ui/card/index.ts';
    import { Skeleton } from '#comp/ui/skeleton/index.ts';
    import { ProjectFilter } from '#features/events/components/filters/index.ts';
    import { openSupportChat } from '#features/intercom/chat.ts';
    import { getIntercom } from '#features/intercom/index.ts';
    import ApiKeyCard from '#features/onboarding/components/api-key-card.svelte';
    import ExieGuide from '#features/onboarding/components/exie-guide.svelte';
    import FirstEventCelebration from '#features/onboarding/components/first-event-celebration.svelte';
    import ListeningStatus from '#features/onboarding/components/listening-status.svelte';
    import OnboardingStepper from '#features/onboarding/components/onboarding-stepper.svelte';
    import PlatformInstructions from '#features/onboarding/components/platform-instructions.svelte';
    import PlatformPicker from '#features/onboarding/components/platform-picker.svelte';
    import SampleDataButton from '#features/onboarding/components/sample-data-button.svelte';
    import { findPlatform, type Platform } from '#features/onboarding/platforms.ts';
    import { resolveClientSetupServerUrls } from '#features/onboarding/server-url.ts';
    import { organization } from '#features/organizations/context.svelte.ts';
    import { useHideOrganizationNotifications } from '#features/organizations/hooks/use-hide-organization-notifications.svelte.ts';
    import { createProductTourActions } from '#features/product-tours/actions.svelte.ts';
    import ProductTourSpotlight from '#features/product-tours/components/product-tour-spotlight.svelte';
    import { productTourCheckpoint } from '#features/product-tours/state.svelte.ts';
    import { getProjectQuery } from '#features/projects/api.svelte.ts';
    import { PUBLIC_BASE_URL, PUBLIC_EXCEPTIONLESS_SERVER_URL } from '#features/shared/environment.ts';
    import { getProjectDefaultTokenQuery, patchToken } from '#features/tokens/api.svelte.ts';
    import EnableTokenDialog from '#features/tokens/components/dialogs/enable-token-dialog.svelte';
    import { ChangeType, type WebSocketMessageValue } from '#features/websockets/models.ts';
    import { createQueryParameters } from '#shared/query-params/index.ts';

    import { redirectToEventsWithFilter } from '../../../redirect-to-events.svelte';

    useHideOrganizationNotifications();

    const projectId = $derived(page.params.projectId || '');

    const projectQuery = getProjectQuery({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    const defaultTokenQuery = getProjectDefaultTokenQuery({
        route: {
            get projectId() {
                return projectId;
            }
        }
    });

    const serverUrls = resolveClientSetupServerUrls({
        baseUrl: PUBLIC_BASE_URL,
        configuredServerUrl: PUBLIC_EXCEPTIONLESS_SERVER_URL,
        origin: window.location.origin,
        showServerUrl: PUBLIC_EXCEPTIONLESS_CLIENT_SETUP_SHOW_SERVER_URL
    });

    const apiKey = $derived(defaultTokenQuery.data?.id);
    const isTokenDisabled = $derived(defaultTokenQuery.data?.is_disabled ?? false);
    const isTokenSuspended = $derived(defaultTokenQuery.data?.is_suspended ?? false);

    let toastId = $state<number | string>();
    let openEnableTokenDialog = $state(false);
    let firstEventReceived = $state(false);
    let celebrationOpen = $state(false);
    let sampleDataRequested = $state(false);
    let isTakingAWhile = $state(false);
    let troubleshootingOpen = $state(false);

    // SvelteKit reuses this page when only the project changes (e.g. from the command palette), so start each project fresh.
    $effect(() => {
        void projectId;
        return () => {
            firstEventReceived = false;
            celebrationOpen = false;
            sampleDataRequested = false;
            isTakingAWhile = false;
            troubleshootingOpen = false;
        };
    });

    const tourActions = createProductTourActions();
    const projectConfigureCheckpoint = $derived(productTourCheckpoint.current?.tourName === 'project-configure' ? productTourCheckpoint.current : undefined);

    const enableTokenMutation = patchToken({
        route: {
            get id() {
                return defaultTokenQuery.data?.id || '';
            }
        }
    });

    async function enableToken() {
        toast.dismiss(toastId);

        try {
            await enableTokenMutation.mutateAsync({
                is_disabled: false
            });
            toastId = toast.success(`Successfully enabled API key`);
        } catch (error) {
            toastId = toast.error('Failed to enable API key. Please try again.');
            throw error;
        }
    }

    // `redirect` is still accepted from older links and tours; waiting no longer depends on it.
    const queryParams = createQueryParameters({
        defaults: {
            redirect: false,
            type: undefined
        },
        history: 'push',
        schema: {
            redirect: 'boolean',
            type: 'string'
        }
    });

    const selectedPlatform = $derived(findPlatform(queryParams.type));

    $effect(() => {
        if (queryParams.type && !selectedPlatform) {
            queryParams.type = null;
        }
    });

    $effect(() => {
        if (projectConfigureCheckpoint?.checkpointName === 'choose-platform' && selectedPlatform) {
            productTourCheckpoint.advance(projectConfigureCheckpoint, 'sdk-instructions');
        }
    });

    /** `waiting` until the project's first event; `received` when it arrives while this page is open. */
    const eventStatus = $derived.by((): 'loading' | 'received' | 'receiving' | 'waiting' => {
        if (firstEventReceived) {
            return 'received';
        }

        if (!projectQuery.data) {
            return 'loading';
        }

        return projectQuery.data.is_configured === false ? 'waiting' : 'receiving';
    });

    const exieMood = $derived.by(() => {
        if (eventStatus === 'received') {
            return 'celebrate' as const;
        }

        if (eventStatus === 'waiting' && isTakingAWhile) {
            return 'thinking' as const;
        }

        if (eventStatus === 'waiting' && selectedPlatform) {
            return 'listening' as const;
        }

        return selectedPlatform ? ('idle' as const) : ('hello' as const);
    });

    async function selectPlatform(platform: Platform): Promise<void> {
        queryParams.type = platform.id;
        await tick();

        // The picker collapses, so bring the steps into view and move focus with them.
        const target = document.getElementById('platform-instructions-heading') ?? document.querySelector<HTMLElement>('[data-api-key-card]');
        const reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
        target?.scrollIntoView({
            behavior: reduceMotion ? 'auto' : 'smooth',
            block: 'start'
        });
        target?.focus({
            preventScroll: true
        });
    }

    useEventListener(document, 'PersistentEventChanged', async (event) => {
        const message = (event as CustomEvent<WebSocketMessageValue<'PersistentEventChanged'>>).detail;
        if (message.project_id !== projectId || message.change_type === ChangeType.Removed) {
            return;
        }

        const checkpoint = projectConfigureCheckpoint;
        if (eventStatus === 'waiting') {
            firstEventReceived = true;
            celebrationOpen = true;
        }

        if (checkpoint) {
            await tourActions.complete(checkpoint);
        }
    });

    const intercom = getIntercom();

    async function goToProjectEvents() {
        celebrationOpen = false;
        await redirectToEventsWithFilter(organization.current, new ProjectFilter([projectId]));
    }

    function openChat() {
        openSupportChat(intercom);
    }
</script>

{#snippet troubleshootingNudge()}
    <Button onclick={() => (troubleshootingOpen = true)} size="sm" variant="outline">Show me</Button>
{/snippet}

<div class="flex flex-col gap-6">
    <div class="flex flex-col gap-4">
        <ExieGuide actions={eventStatus === 'waiting' && isTakingAWhile && !troubleshootingOpen ? troubleshootingNudge : undefined} mood={exieMood} size={64}>
            {#if eventStatus === 'received'}
                {sampleDataRequested ? 'Sample events are rolling in. Take a look around!' : 'Your first event just landed. Nice work!'}
            {:else if eventStatus === 'waiting' && isTakingAWhile}
                Taking a while? I've got a few tips.
            {:else if selectedPlatform}
                {selectedPlatform.tip}
            {:else if eventStatus === 'receiving'}
                This project already sends events. Connecting another app? Pick its platform and I'll show you what to paste.
            {:else}
                Which platform is your app built on? Pick one and I'll show you exactly what to paste.
            {/if}
        </ExieGuide>
        <OnboardingStepper
            current={eventStatus === 'waiting' || eventStatus === 'loading' ? 'install-client' : 'catch-event'}
            currentComplete={eventStatus === 'received' || eventStatus === 'receiving'}
        />
    </div>

    {#if eventStatus === 'waiting'}
        <div data-tour="project-first-event">
            <ListeningStatus bind:isTakingAWhile bind:troubleshootingOpen platform={selectedPlatform} serverUrl={serverUrls.submissionServerUrl}>
                {#snippet actions()}
                    {#if projectConfigureCheckpoint && projectConfigureCheckpoint.checkpointName !== 'choose-platform'}
                        <Button onclick={() => projectConfigureCheckpoint && tourActions.dismiss(projectConfigureCheckpoint)} size="sm" variant="ghost"
                            >End guide</Button
                        >
                    {/if}
                    <SampleDataButton onQueued={() => (sampleDataRequested = true)} {projectId} projectName={projectQuery.data?.name} size="sm" />
                {/snippet}
            </ListeningStatus>
        </div>
    {:else if eventStatus === 'receiving' || eventStatus === 'received'}
        <Card.Root data-receiving-events size="sm">
            <Card.Content class="flex flex-wrap items-center gap-3">
                <CircleCheck aria-hidden="true" class="text-primary size-5 shrink-0" />
                <p class="text-foreground min-w-0 flex-1 text-sm font-medium" role="status">This project is receiving events ✓</p>
                <Button onclick={goToProjectEvents} size="sm" variant="outline">View events</Button>
            </Card.Content>
        </Card.Root>
    {:else}
        <Skeleton class="h-14 w-full" />
    {/if}

    <PlatformPicker onSelect={selectPlatform} selectedId={selectedPlatform?.id} />

    {#if projectConfigureCheckpoint?.checkpointName === 'choose-platform'}
        <ProductTourSpotlight
            checkpoint={projectConfigureCheckpoint}
            description="Choose your application's language or platform to see the setup instructions."
            showProgress={false}
            onDismiss={tourActions.dismiss}
            target="[data-tour='project-configure-platform']"
            title="Choose your language or platform"
        />
    {/if}

    <ApiKeyCard
        {apiKey}
        isDisabled={isTokenDisabled}
        isError={defaultTokenQuery.isError}
        isLoading={defaultTokenQuery.isPending}
        isSuspended={isTokenSuspended}
        onContactSupport={openChat}
        onEnable={() => (openEnableTokenDialog = true)}
        onRetry={() => defaultTokenQuery.refetch()}
    />

    {#if selectedPlatform}
        {#if apiKey}
            <PlatformInstructions
                context={{
                    apiKey,
                    ...serverUrls
                }}
                platform={selectedPlatform}
            />
        {:else if defaultTokenQuery.isPending}
            <div class="flex flex-col gap-3" aria-label="Loading setup instructions">
                <Skeleton class="h-5 w-48" />
                <Skeleton class="h-20 w-full" />
                <Skeleton class="h-32 w-full" />
            </div>
        {:else}
            <P class="text-muted-foreground text-sm">Setup instructions appear once your API key loads.</P>
        {/if}

        {#if selectedPlatform.category === 'JavaScript'}
            <Notification>
                <NotificationTitle>Improve production stack traces</NotificationTitle>
                <NotificationDescription>
                    <P>
                        Exceptionless automatically discovers public source maps. If your source maps are private or are not published with your generated
                        JavaScript, <A
                            href={resolve('/(app)/project/[projectId]/source-maps', {
                                projectId
                            })}>upload and manage them</A
                        > to restore the original function names and source locations.
                    </P>
                </NotificationDescription>
            </Notification>
        {/if}
    {/if}

    <div class="border-border flex flex-col-reverse gap-2 border-t pt-4 sm:flex-row sm:flex-wrap sm:justify-end">
        <!-- First-run setup (redirect=true) never came from Project Settings. -->
        {#if !queryParams.redirect}
            <Button
                variant="ghost"
                href={resolve('/(app)/project/[projectId]/manage', {
                    projectId
                })}
            >
                <ArrowLeft data-icon="inline-start" aria-hidden="true" /> Back to Project Settings
            </Button>
        {/if}
        <Button variant="ghost" href={`${resolve('/(app)/account/notifications')}?project=${projectId}`}>
            <NotificationSettings data-icon="inline-start" aria-hidden="true" /> Notifications
        </Button>
        <Button variant="outline" href={resolve('/(app)/account/ai-tools')}>
            <Bot data-icon="inline-start" aria-hidden="true" /> Set Up AI Tools
        </Button>
        <Button onclick={goToProjectEvents}>
            <Events data-icon="inline-start" aria-hidden="true" /> View Events
        </Button>
    </div>

    {#if isTokenDisabled}
        <EnableTokenDialog bind:open={openEnableTokenDialog} id={defaultTokenQuery.data?.id || ''} notes={defaultTokenQuery.data?.notes} enable={enableToken} />
    {/if}
</div>

<FirstEventCelebration bind:open={celebrationOpen} isSampleData={sampleDataRequested} onSeeAllEvents={goToProjectEvents} {projectId} />
