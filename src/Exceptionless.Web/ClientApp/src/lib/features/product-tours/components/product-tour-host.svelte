<script lang="ts">
    import type { AssistantAccess } from '$features/assistant/models';
    import type { ViewCurrentUser } from '$features/users/models';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { getOrganizationEventsQuery } from '$features/events/api.svelte';
    import { getOrganizationProjectsQuery } from '$features/projects/api.svelte';
    import { putCurrentUserProductTour } from '$features/users/api.svelte';
    import { onMount } from 'svelte';
    import { toast } from 'svelte-sonner';

    import type { ProductTourContext, ProductTourListItem, ProductTourName } from '../models';

    import { createProductTourActions } from '../actions.svelte';
    import { getProductTourItems, getRecommendedProductTourName } from '../catalog';
    import { shouldOfferProductTourInvitation } from '../eligibility';
    import { productTourCheckpoint } from '../state.svelte';
    import ProductTourFeatureAnnouncement from './alerts/product-tour-feature-announcement.svelte';
    import ProductTourWelcome from './alerts/product-tour-welcome.svelte';
    import ProductTourCatalogDialog from './dialogs/product-tour-catalog-dialog.svelte';
    import ProductTourShellSpotlight from './product-tour-shell-spotlight.svelte';

    interface Props {
        assistantAccess?: AssistantAccess;
        closeOverlays: () => void;
        currentUser?: ViewCurrentUser;
        isAnyOverlayOpen: boolean;
        isImpersonating: boolean;
        isMobile: boolean;
        isSetupPage: boolean;
        openAssistant: () => Promise<void>;
        organizationId?: string;
        pathname: string;
        setMobileNavigationOpen: (open: boolean) => void;
        stateSettled: boolean;
    }

    const EVENT_PATH = resolve('/(app)/event');
    const STACK_PATH = resolve('/(app)/stack');
    const SYSTEM_PATH = resolve('/(app)/system');

    let {
        assistantAccess,
        closeOverlays,
        currentUser,
        isAnyOverlayOpen,
        isImpersonating,
        isMobile,
        isSetupPage,
        openAssistant,
        organizationId,
        pathname,
        setMobileNavigationOpen,
        stateSettled
    }: Props = $props();

    let catalogOpen = $state(false);
    let automaticSurface = $state<'exie-announcement' | 'handled' | 'welcome'>();
    let automaticSurfaceReady = $state(false);
    let automaticSurfaceUserId = $state<string>();

    const actions = createProductTourActions();
    const progressMutation = putCurrentUserProductTour();
    const projectsQuery = getOrganizationProjectsQuery({
        route: {
            get organizationId() {
                return organizationId;
            }
        }
    });
    const projects = $derived(projectsQuery.data?.data ?? undefined);
    const projectConfigurePage = $derived(page.route.id === '/(app)/project/[projectId]/configure');
    const errorEventsQuery = getOrganizationEventsQuery({
        enabled: () => catalogOpen,
        params: {
            filter: 'type:error',
            limit: 1,
            mode: 'summary',
            time: 'all'
        },
        route: {
            get organizationId() {
                return organizationId;
            }
        }
    });
    const errorEventAvailability = $derived.by((): ProductTourContext['errorEventAvailability'] => {
        if (!organizationId || !catalogOpen || errorEventsQuery.isPending) {
            return 'loading';
        }

        if (errorEventsQuery.isError) {
            return 'error';
        }

        return errorEventsQuery.data?.data?.length ? 'available' : 'empty';
    });
    const hostStateSettled = $derived(stateSettled && (!organizationId || projectsQuery.isSuccess || projectsQuery.isError));
    const context = $derived<ProductTourContext>({
        assistantAccess,
        errorEventAvailability,
        isProjectConfigurePage: projectConfigurePage,
        isSetupPage,
        organizationId,
        pathname,
        projects
    });
    const items = $derived(getProductTourItems(context, currentUser?.product_tours));
    const recommended = $derived(items.find((item) => item.name === getRecommendedProductTourName(context)) ?? items[0]!);
    const checkpoint = $derived(productTourCheckpoint.current);
    const canShowInvitation = $derived(
        hostStateSettled && !!currentUser && !checkpoint && !catalogOpen && !isAnyOverlayOpen && !isImpersonating && !isSetupPage
    );
    const welcomeEligible = $derived(shouldOfferProductTourInvitation(currentUser?.product_tours?.app_welcome));
    const welcomeOpen = $derived(canShowInvitation && automaticSurface === 'welcome' && !pathname.startsWith(SYSTEM_PATH) && welcomeEligible);
    const exieAnnouncementOpen = $derived(
        !!(
            canShowInvitation &&
            automaticSurface === 'exie-announcement' &&
            assistantAccess?.enabled &&
            (pathname.startsWith(EVENT_PATH) || pathname.startsWith(STACK_PATH)) &&
            !welcomeEligible &&
            shouldOfferProductTourInvitation(currentUser?.product_tours?.exie_announcement)
        )
    );

    onMount(() => {
        automaticSurfaceReady = true;
    });

    $effect(() => {
        if (!automaticSurfaceReady || !currentUser) {
            automaticSurface = undefined;
            automaticSurfaceUserId = undefined;
            return;
        }

        if (automaticSurfaceUserId !== currentUser.id) {
            automaticSurface = undefined;
            automaticSurfaceUserId = currentUser.id;

            return;
        }

        if (automaticSurface || !hostStateSettled || isImpersonating || isSetupPage || checkpoint) {
            return;
        }

        if (welcomeEligible && !pathname.startsWith(SYSTEM_PATH)) {
            automaticSurface = 'welcome';
            return;
        }

        if (
            assistantAccess?.enabled &&
            (pathname.startsWith(EVENT_PATH) || pathname.startsWith(STACK_PATH)) &&
            shouldOfferProductTourInvitation(currentUser.product_tours?.exie_announcement)
        ) {
            automaticSurface = 'exie-announcement';
        }
    });

    $effect(() => {
        if (!stateSettled) {
            return;
        }

        if (!currentUser) {
            productTourCheckpoint.clear();
            return;
        }

        const active = productTourCheckpoint.current;
        if (
            active?.userId === currentUser.id &&
            active.tourName === 'project-configure' &&
            !active.organizationId &&
            organizationId &&
            pathname === resolve('/(app)/organization/add')
        ) {
            productTourCheckpoint.advance(active, active.checkpointName, organizationId);
            return;
        }

        if (active && (active.userId !== currentUser.id || active.organizationId !== organizationId)) {
            productTourCheckpoint.clear(active);
        }
    });

    export async function openCatalog(): Promise<void> {
        const active = checkpoint;
        if (active?.tourName === 'app-overview' && active.checkpointName === 'help' && !(await actions.complete(active))) {
            return;
        }

        closeOverlays();
        catalogOpen = true;
    }

    export async function startTour(name: ProductTourName): Promise<void> {
        if (!currentUser) {
            return;
        }

        const item = getItem(name);
        if (!item.currentAvailability.available) {
            await openCatalog();
            return;
        }

        automaticSurface = 'handled';
        const active = productTourCheckpoint.current;
        if (active?.tourName === name && isActiveTourRenderable(active)) {
            closeOverlays();
            catalogOpen = false;
            return;
        }

        if (active) {
            productTourCheckpoint.clear(active);
        }

        closeOverlays();
        catalogOpen = false;
        const start = item.start({
            ...context,
            search: window.location.search
        });
        const expectedUserId = currentUser.id;
        const expectedOrganizationId = organizationId;
        const next = productTourCheckpoint.start(name, start.checkpointName, currentUser.id, organizationId);

        const destination = start.route;
        if (`${pathname}${window.location.search}` !== destination) {
            await goto(destination);
        }

        if (productTourCheckpoint.current !== next || currentUser?.id !== expectedUserId || organizationId !== expectedOrganizationId) {
            return;
        }

        if (next.tourName === 'exie-overview' && next.checkpointName === 'open-exie') {
            setMobileNavigationOpen(false);
        }
    }

    async function recordPreference(name: 'app-welcome' | 'exie-announcement'): Promise<boolean> {
        if (!currentUser) {
            return false;
        }
        const userId = currentUser.id;
        automaticSurface = 'handled';
        void progressMutation
            .mutateAsync({
                tourName: name,
                userId
            })
            .catch(() => {
                if (currentUser?.id === userId) {
                    toast.error('We could not save your guided-tour preference. Please try again.');
                }
            });
        return true;
    }

    async function onWelcomeStart(): Promise<void> {
        if (!(await recordPreference('app-welcome'))) {
            return;
        }

        await startTour(recommended.name);
    }

    async function onWelcomeBrowse(): Promise<void> {
        if (!(await recordPreference('app-welcome'))) {
            return;
        }

        await openCatalog();
    }

    async function onWelcomeSkip(): Promise<void> {
        await recordPreference('app-welcome');
    }

    async function onExieAnnouncementStart(): Promise<void> {
        if (!(await recordPreference('exie-announcement'))) {
            return;
        }

        if (assistantAccess?.has_access) {
            await startTour('exie-overview');
        } else {
            await openAssistant();
        }
    }

    async function onExieAnnouncementDismiss(): Promise<void> {
        await recordPreference('exie-announcement');
    }

    function getItem(name: ProductTourName): ProductTourListItem {
        return items.find((item) => item.name === name)!;
    }

    function isActiveTourRenderable(active: NonNullable<typeof checkpoint>): boolean {
        return getItem(active.tourName).canResume(active.checkpointName, page.route.id);
    }
</script>

<ProductTourWelcome open={welcomeOpen} onBrowse={onWelcomeBrowse} onDismiss={onWelcomeSkip} onStart={onWelcomeStart} {recommended} />

{#if exieAnnouncementOpen && assistantAccess}
    <ProductTourFeatureAnnouncement
        hasAccess={assistantAccess.has_access}
        message={assistantAccess.message}
        onDismiss={onExieAnnouncementDismiss}
        onStart={onExieAnnouncementStart}
    />
{/if}

<ProductTourCatalogDialog
    activeTourName={checkpoint?.tourName}
    bind:open={catalogOpen}
    {items}
    onStart={startTour}
    ready={stateSettled && !!currentUser}
    resumableTourName={checkpoint && isActiveTourRenderable(checkpoint) ? checkpoint.tourName : undefined}
/>

{#if checkpoint && (checkpoint.tourName === 'exie-overview' || checkpoint.tourName === 'app-overview')}
    {#key checkpoint}
        <ProductTourShellSpotlight {assistantAccess} {checkpoint} {isAnyOverlayOpen} {isMobile} {openAssistant} {setMobileNavigationOpen} />
    {/key}
{/if}
