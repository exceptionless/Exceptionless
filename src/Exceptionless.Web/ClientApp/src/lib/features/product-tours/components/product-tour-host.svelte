<script lang="ts">
    import type { AssistantAccess } from '$features/assistant/models';
    import type { ViewCurrentUser } from '$features/users/models';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { getOrganizationEventsQuery } from '$features/events/api.svelte';
    import { getOrganizationProjectsQuery } from '$features/projects/api.svelte';
    import { putCurrentUserProductTour } from '$features/users/api.svelte';
    import { ProductTourStatus } from '$features/users/models';
    import { onMount } from 'svelte';
    import { toast } from 'svelte-sonner';

    import type { ProductTourCheckpoint, ProductTourContext, ProductTourLaunchSource, ProductTourListItem, ProductTourName } from '../models';

    import { createProductTourActions } from '../actions.svelte';
    import { createProductTourActivity } from '../api.svelte';
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
    const EXIE_ANNOUNCEMENT_VERSION = 1;
    const ORGANIZATION_ADD_PATH = resolve('/(app)/organization/add');
    const PROJECT_ADD_PATH = resolve('/(app)/project/add');
    const STACK_PATH = resolve('/(app)/stack');
    const SYSTEM_PATH = resolve('/(app)/system');
    const WELCOME_VERSION = 1;

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
    let catalogSource = $state<ProductTourLaunchSource>('catalog');
    let checkErrorAvailability = $state(false);
    let automaticSurface = $state<'exie-announcement' | 'welcome'>();
    let automaticSurfaceReady = $state(false);
    let automaticSurfaceClaimed = $state(false);
    let automaticSurfaceUserId = $state<string>();
    let lastTrackedAnnouncementImpression = $state('');
    let lastTrackedWelcomeImpression = $state('');
    let welcomeHandled = $state(false);
    let attemptedProjectCompletion: ProductTourCheckpoint | undefined;

    const actions = createProductTourActions();
    const track = createProductTourActivity();
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
        enabled: () => checkErrorAvailability,
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
    const errorEventAvailability = $derived<ProductTourContext['errorEventAvailability']>(
        !organizationId || !checkErrorAvailability || errorEventsQuery.isPending
            ? 'loading'
            : errorEventsQuery.isError
              ? 'error'
              : (errorEventsQuery.data?.data?.length ?? 0) > 0
                ? 'available'
                : 'empty'
    );
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
    const welcomeOpen = $derived(
        !!(
            hostStateSettled &&
            automaticSurface === 'welcome' &&
            currentUser &&
            !welcomeHandled &&
            !catalogOpen &&
            !isAnyOverlayOpen &&
            !isImpersonating &&
            !isSetupPage &&
            !pathname.startsWith(SYSTEM_PATH) &&
            shouldOfferProductTourInvitation(currentUser.product_tours?.['app-welcome'], WELCOME_VERSION)
        )
    );
    const exieAnnouncementOpen = $derived(
        !!(
            hostStateSettled &&
            automaticSurface === 'exie-announcement' &&
            currentUser &&
            assistantAccess?.enabled &&
            (pathname.startsWith(EVENT_PATH) || pathname.startsWith(STACK_PATH)) &&
            !isSetupPage &&
            !isImpersonating &&
            !checkpoint &&
            !catalogOpen &&
            !isAnyOverlayOpen &&
            !shouldOfferProductTourInvitation(currentUser.product_tours?.['app-welcome'], WELCOME_VERSION) &&
            shouldOfferProductTourInvitation(currentUser.product_tours?.['exie-announcement'], EXIE_ANNOUNCEMENT_VERSION)
        )
    );

    onMount(() => {
        automaticSurfaceReady = true;
    });

    $effect(() => {
        if (!automaticSurfaceReady || !currentUser) {
            automaticSurface = undefined;
            automaticSurfaceUserId = undefined;
            automaticSurfaceClaimed = false;
            return;
        }

        if (automaticSurfaceUserId !== currentUser.id) {
            automaticSurface = undefined;
            automaticSurfaceUserId = currentUser.id;
            try {
                automaticSurfaceClaimed = sessionStorage.getItem(getAutomaticSurfaceKey(currentUser.id)) === 'shown';
            } catch {
                automaticSurfaceClaimed = false;
            }
            welcomeHandled = false;
            return;
        }

        if (automaticSurfaceClaimed || !hostStateSettled || isImpersonating || isSetupPage) {
            return;
        }

        if (shouldOfferProductTourInvitation(currentUser.product_tours?.['app-welcome'], WELCOME_VERSION) && !pathname.startsWith(SYSTEM_PATH)) {
            claimAutomaticSurface('welcome');
            return;
        }

        if (
            assistantAccess?.enabled &&
            (pathname.startsWith(EVENT_PATH) || pathname.startsWith(STACK_PATH)) &&
            shouldOfferProductTourInvitation(currentUser.product_tours?.['exie-announcement'], EXIE_ANNOUNCEMENT_VERSION)
        ) {
            claimAutomaticSurface('exie-announcement');
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

        const active = productTourCheckpoint.current ?? productTourCheckpoint.restore(currentUser.id, organizationId);
        if (active && (active.userId !== currentUser.id || active.organizationId !== organizationId)) {
            productTourCheckpoint.clear(active);
        }
    });

    $effect(() => {
        const active = checkpoint;
        if (active?.tourName === 'project-configure' && active.checkpointName === 'event-received' && projectConfigurePage) {
            if (attemptedProjectCompletion !== active) {
                attemptedProjectCompletion = active;
                actions.completeAfterDomainSuccess(active);
            }
        } else {
            attemptedProjectCompletion = undefined;
        }
    });

    $effect(() => {
        if (!currentUser) {
            return;
        }

        const impression = `${currentUser.id}:${WELCOME_VERSION}`;
        if (welcomeOpen && lastTrackedWelcomeImpression !== impression) {
            lastTrackedWelcomeImpression = impression;
            void track('shown', 'app-welcome', WELCOME_VERSION, 'welcome');
        }
    });

    $effect(() => {
        if (!currentUser) {
            return;
        }

        const impression = `${currentUser.id}:${EXIE_ANNOUNCEMENT_VERSION}`;
        if (exieAnnouncementOpen && lastTrackedAnnouncementImpression !== impression) {
            lastTrackedAnnouncementImpression = impression;
            void track('shown', 'exie-announcement', EXIE_ANNOUNCEMENT_VERSION, 'feature-announcement');
        }
    });

    export async function openCatalog(source: ProductTourLaunchSource = 'catalog'): Promise<void> {
        const active = checkpoint;
        if (active?.tourName === 'app-overview' && active.checkpointName === 'help' && !(await actions.complete(active))) {
            return;
        }
        closeOverlays();
        checkErrorAvailability = true;
        catalogSource = source;
        catalogOpen = true;
    }

    export async function startTour<Name extends ProductTourName>(name: Name, source: ProductTourLaunchSource = 'catalog'): Promise<void> {
        if (!currentUser) {
            return;
        }
        const item = getItem(name);
        if (!item.currentAvailability.available) {
            await openCatalog(source);
            return;
        }

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
        const next = productTourCheckpoint.start(name, start.checkpointName, source, currentUser.id, item.version, organizationId);
        void track('started', name, item.version, source);

        const destination = start.route;
        if (`${pathname}${window.location.search}` !== destination) {
            await goto(destination);
        }

        if (next.tourName === 'exie-overview' && next.checkpointName === 'open-exie') {
            setMobileNavigationOpen(false);
        }
    }

    async function recordPreference(name: 'app-welcome' | 'exie-announcement', version: number, status: ProductTourStatus): Promise<boolean> {
        if (progressMutation.isPending) {
            return false;
        }
        try {
            await progressMutation.mutateAsync({
                progress: {
                    status,
                    version
                },
                tourName: name
            });
            return true;
        } catch {
            toast.error('We could not save your guided-tour preference. Please try again.');
            return false;
        }
    }

    async function onWelcomeStart(): Promise<void> {
        if (!(await recordPreference('app-welcome', WELCOME_VERSION, ProductTourStatus.Completed))) {
            return;
        }
        welcomeHandled = true;
        automaticSurface = undefined;
        void track('completed', 'app-welcome', WELCOME_VERSION, 'welcome');
        await startTour(recommended.name, 'welcome');
    }

    async function onWelcomeBrowse(): Promise<void> {
        if (!(await recordPreference('app-welcome', WELCOME_VERSION, ProductTourStatus.Completed))) {
            return;
        }
        welcomeHandled = true;
        automaticSurface = undefined;
        void track('completed', 'app-welcome', WELCOME_VERSION, 'welcome');
        await openCatalog('catalog');
    }

    async function onWelcomeSkip(): Promise<void> {
        if (!(await recordPreference('app-welcome', WELCOME_VERSION, ProductTourStatus.Dismissed))) {
            return;
        }
        welcomeHandled = true;
        automaticSurface = undefined;
        void track('dismissed', 'app-welcome', WELCOME_VERSION, 'welcome');
    }

    async function onExieAnnouncementStart(): Promise<void> {
        if (!(await recordPreference('exie-announcement', EXIE_ANNOUNCEMENT_VERSION, ProductTourStatus.Completed))) {
            return;
        }
        automaticSurface = undefined;
        void track('completed', 'exie-announcement', EXIE_ANNOUNCEMENT_VERSION, 'feature-announcement');
        if (assistantAccess?.has_access) {
            await startTour('exie-overview', 'feature-announcement');
        } else {
            await openAssistant();
        }
    }

    async function onExieAnnouncementDismiss(): Promise<void> {
        if (!(await recordPreference('exie-announcement', EXIE_ANNOUNCEMENT_VERSION, ProductTourStatus.Dismissed))) {
            return;
        }
        automaticSurface = undefined;
        void track('dismissed', 'exie-announcement', EXIE_ANNOUNCEMENT_VERSION, 'feature-announcement');
    }

    function getItem<Name extends ProductTourName>(name: Name): ProductTourListItem<Name> {
        return items.find((item) => item.name === name)! as ProductTourListItem<Name>;
    }

    function claimAutomaticSurface(surface: 'exie-announcement' | 'welcome'): void {
        if (!currentUser) {
            return;
        }

        automaticSurface = surface;
        automaticSurfaceClaimed = true;
        try {
            sessionStorage.setItem(getAutomaticSurfaceKey(currentUser.id), 'shown');
        } catch {
            // Keep the in-memory claim when browser storage is unavailable.
        }
    }

    function getAutomaticSurfaceKey(userId: string): string {
        return `exceptionless.product-tour.automatic-surface.${userId}.welcome-v${WELCOME_VERSION}.announcement-v${EXIE_ANNOUNCEMENT_VERSION}`;
    }

    function isActiveTourRenderable(active: NonNullable<typeof checkpoint>): boolean {
        switch (active.tourName) {
            case 'app-overview':
                return true;
            case 'event-investigate':
                return pathname.startsWith(EVENT_PATH) && (active.checkpointName === 'filter-errors' || active.checkpointName === 'choose-error');
            case 'exie-overview':
                return active.checkpointName === 'open-exie';
            case 'project-configure':
                if (active.checkpointName === 'organization-name') {
                    return pathname === ORGANIZATION_ADD_PATH;
                }

                if (active.checkpointName === 'project-name') {
                    return pathname === ORGANIZATION_ADD_PATH || pathname === PROJECT_ADD_PATH;
                }
                return pathname.startsWith(PROJECT_ADD_PATH.slice(0, -3)) && pathname.endsWith('/configure');
            case 'saved-view-create':
                return pathname.startsWith(EVENT_PATH) && (active.checkpointName === 'open-view-menu' || active.checkpointName === 'view-created');
        }
    }
</script>

<ProductTourWelcome
    busy={progressMutation.isPending}
    open={welcomeOpen}
    onBrowse={onWelcomeBrowse}
    onDismiss={onWelcomeSkip}
    onStart={onWelcomeStart}
    {recommended}
/>

{#if exieAnnouncementOpen && assistantAccess}
    <ProductTourFeatureAnnouncement
        hasAccess={assistantAccess.has_access}
        message={assistantAccess.message}
        busy={progressMutation.isPending}
        onDismiss={onExieAnnouncementDismiss}
        onStart={onExieAnnouncementStart}
    />
{/if}

<ProductTourCatalogDialog
    activeTourName={checkpoint?.tourName}
    bind:open={catalogOpen}
    {items}
    onStart={(name) => startTour(name, catalogSource)}
    ready={hostStateSettled && !!currentUser}
    resumableTourName={checkpoint && isActiveTourRenderable(checkpoint) ? checkpoint.tourName : undefined}
/>

{#if checkpoint && (checkpoint.tourName === 'exie-overview' || checkpoint.tourName === 'app-overview')}
    {#key checkpoint}
        <ProductTourShellSpotlight {assistantAccess} {checkpoint} {isAnyOverlayOpen} {isMobile} {openAssistant} {setMobileNavigationOpen} />
    {/key}
{/if}
