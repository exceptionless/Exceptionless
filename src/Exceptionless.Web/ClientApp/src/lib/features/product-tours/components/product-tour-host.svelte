<script lang="ts">
    import type { AssistantAccess } from '$features/assistant/models';
    import type { ViewProject } from '$features/projects/models';
    import type { ViewCurrentUser } from '$features/users/models';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { putCurrentUserProductTour } from '$features/users/api.svelte';
    import { ProductTourStatus } from '$features/users/models';
    import { toast } from 'svelte-sonner';

    import type { ProductTourContext, ProductTourLaunchSource, ProductTourListItem, ProductTourName } from '../types';

    import { createProductTourActions, track } from '../actions.svelte';
    import { getProductTourItems, getRecommendedProductTourName } from '../catalog';
    import { shouldOfferProductTourAnnouncement, shouldOfferProductTourWelcome } from '../eligibility';
    import { productTourCheckpoint } from '../state.svelte';
    import ProductTourCatalogDialog from './dialogs/product-tour-catalog-dialog.svelte';
    import ProductTourWelcomeDialog from './dialogs/product-tour-welcome-dialog.svelte';
    import ProductTourFeatureAnnouncement from './product-tour-feature-announcement.svelte';
    import ProductTourShellSpotlight from './product-tour-shell-spotlight.svelte';

    interface Props {
        assistantAccess?: AssistantAccess;
        closeOverlays: () => void;
        currentUser?: ViewCurrentUser;
        errorEventAvailability: ProductTourContext['errorEventAvailability'];
        isAnyOverlayOpen: boolean;
        isImpersonating: boolean;
        isSetupPage: boolean;
        openAssistant: () => Promise<void>;
        organizationId?: string;
        pathname: string;
        projects: ViewProject[];
        requestErrorAvailability: () => void;
        setMobileNavigationOpen: (open: boolean) => void;
        stateSettled: boolean;
    }

    const WELCOME_VERSION = 1;
    const EXIE_ANNOUNCEMENT_VERSION = 1;
    const SYSTEM_PATH = resolve('/(app)/system');

    let {
        assistantAccess,
        closeOverlays,
        currentUser,
        errorEventAvailability,
        isAnyOverlayOpen,
        isImpersonating,
        isSetupPage,
        openAssistant,
        organizationId,
        pathname,
        projects,
        requestErrorAvailability,
        setMobileNavigationOpen,
        stateSettled
    }: Props = $props();

    let catalogOpen = $state(false);
    let catalogSource = $state<ProductTourLaunchSource>('catalog');
    let lastTrackedAnnouncementImpression = $state('');
    let lastTrackedWelcomeImpression = $state('');
    let welcomeHandled = $state(false);

    const actions = createProductTourActions();
    const progressMutation = putCurrentUserProductTour();
    const context = $derived<ProductTourContext>({
        assistantAccess,
        errorEventAvailability,
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
            stateSettled &&
            currentUser &&
            !welcomeHandled &&
            !catalogOpen &&
            !isAnyOverlayOpen &&
            !isImpersonating &&
            !isSetupPage &&
            !pathname.startsWith(SYSTEM_PATH) &&
            shouldOfferProductTourWelcome(currentUser.product_tours?.welcome, WELCOME_VERSION)
        )
    );
    const exieAnnouncementOpen = $derived(
        !!(
            stateSettled &&
            currentUser &&
            assistantAccess?.enabled &&
            (pathname.startsWith('/next/event') || pathname.startsWith('/next/stack')) &&
            !isSetupPage &&
            !isImpersonating &&
            !checkpoint &&
            !welcomeOpen &&
            !catalogOpen &&
            !isAnyOverlayOpen &&
            !shouldOfferProductTourWelcome(currentUser.product_tours?.welcome, WELCOME_VERSION) &&
            shouldOfferProductTourAnnouncement(currentUser.product_tours?.['exie-announcement'], EXIE_ANNOUNCEMENT_VERSION)
        )
    );

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
        if (!currentUser) {
            return;
        }

        const impression = `${currentUser.id}:${WELCOME_VERSION}`;
        if (welcomeOpen && lastTrackedWelcomeImpression !== impression) {
            lastTrackedWelcomeImpression = impression;
            void track('shown', 'welcome', WELCOME_VERSION, 'automatic');
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

    export function openCatalog(source: ProductTourLaunchSource = 'catalog'): void {
        closeOverlays();
        requestErrorAvailability();
        catalogSource = source;
        catalogOpen = true;
    }

    export async function startTour(name: ProductTourName, source: ProductTourLaunchSource = 'catalog'): Promise<void> {
        if (!currentUser) {
            return;
        }
        const item = getItem(name);
        if (!item.currentAvailability.available) {
            openCatalog(source);
            return;
        }

        const active = productTourCheckpoint.current;
        if (active && !(await actions.dismiss(active))) {
            return;
        }

        closeOverlays();
        catalogOpen = false;
        const next = productTourCheckpoint.start({
            checkpointName: item.initialCheckpoint,
            organizationId,
            phase: {
                type: 'active'
            },
            source,
            tourName: name,
            userId: currentUser.id
        });
        await Promise.all([track('shown', name, item.version, source), track('started', name, item.version, source)]);

        const destination = item.startingRoute(context);
        if (`${pathname}${window.location.search}` !== destination) {
            await goto(destination);
        }

        if (next.tourName === 'meet-exie' && next.checkpointName === 'open-exie') {
            setMobileNavigationOpen(false);
        }
    }

    async function recordPreference(name: 'exie-announcement' | 'welcome', version: number, status: ProductTourStatus): Promise<boolean> {
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
        if (!(await recordPreference('welcome', WELCOME_VERSION, ProductTourStatus.Completed))) {
            return;
        }
        welcomeHandled = true;
        await track('completed', 'welcome', WELCOME_VERSION, 'automatic');
        await startTour(recommended.name, 'automatic');
    }

    async function onWelcomeBrowse(): Promise<void> {
        if (!(await recordPreference('welcome', WELCOME_VERSION, ProductTourStatus.Completed))) {
            return;
        }
        welcomeHandled = true;
        await track('completed', 'welcome', WELCOME_VERSION, 'automatic');
        openCatalog('catalog');
    }

    async function onWelcomeSkip(): Promise<void> {
        if (!(await recordPreference('welcome', WELCOME_VERSION, ProductTourStatus.Dismissed))) {
            return;
        }
        welcomeHandled = true;
        await track('dismissed', 'welcome', WELCOME_VERSION, 'automatic');
    }

    async function onExieAnnouncementStart(): Promise<void> {
        if (!(await recordPreference('exie-announcement', EXIE_ANNOUNCEMENT_VERSION, ProductTourStatus.Completed))) {
            return;
        }
        await track('completed', 'exie-announcement', EXIE_ANNOUNCEMENT_VERSION, 'feature-announcement');
        await startTour('meet-exie', 'feature-announcement');
    }

    async function onExieAnnouncementDismiss(): Promise<void> {
        if (!(await recordPreference('exie-announcement', EXIE_ANNOUNCEMENT_VERSION, ProductTourStatus.Dismissed))) {
            return;
        }
        await track('dismissed', 'exie-announcement', EXIE_ANNOUNCEMENT_VERSION, 'feature-announcement');
    }

    function getItem(name: ProductTourName): ProductTourListItem {
        return items.find((item) => item.name === name)!;
    }
</script>

<ProductTourWelcomeDialog open={welcomeOpen} onBrowse={onWelcomeBrowse} onDismiss={onWelcomeSkip} onStart={onWelcomeStart} {recommended} />

{#if exieAnnouncementOpen && assistantAccess}
    <ProductTourFeatureAnnouncement
        hasAccess={assistantAccess.has_access}
        message={assistantAccess.message}
        onDismiss={onExieAnnouncementDismiss}
        onStart={onExieAnnouncementStart}
    />
{/if}

<ProductTourCatalogDialog bind:open={catalogOpen} {items} onStart={(name) => startTour(name, catalogSource)} />

{#if checkpoint && (checkpoint.tourName === 'meet-exie' || checkpoint.tourName === 'ui-overview')}
    {#key checkpoint}
        <ProductTourShellSpotlight {assistantAccess} {checkpoint} {isAnyOverlayOpen} {openAssistant} {setMobileNavigationOpen} />
    {/key}
{/if}
