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
    const items = $derived(getProductTourItems(context, currentUser?.product_tour_versions ?? {}, currentUser?.product_tours));
    const recommended = $derived(items.find((item) => item.name === getRecommendedProductTourName(context)) ?? items[0]!);
    const checkpoint = $derived(productTourCheckpoint.current);
    const welcomeVersion = $derived(currentUser?.product_tour_versions.welcome ?? 0);
    const exieAnnouncementVersion = $derived(currentUser?.product_tour_versions['exie-announcement'] ?? 0);
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
            welcomeVersion > 0 &&
            shouldOfferProductTourWelcome(currentUser.product_tours?.welcome, welcomeVersion)
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
            !shouldOfferProductTourWelcome(currentUser.product_tours?.welcome, welcomeVersion) &&
            exieAnnouncementVersion > 0 &&
            shouldOfferProductTourAnnouncement(currentUser.product_tours?.['exie-announcement'], exieAnnouncementVersion)
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

        const impression = `${currentUser.id}:${welcomeVersion}`;
        if (welcomeOpen && lastTrackedWelcomeImpression !== impression) {
            lastTrackedWelcomeImpression = impression;
            void track('shown', 'welcome', welcomeVersion, 'automatic');
        }
    });

    $effect(() => {
        if (!currentUser) {
            return;
        }

        const impression = `${currentUser.id}:${exieAnnouncementVersion}`;
        if (exieAnnouncementOpen && lastTrackedAnnouncementImpression !== impression) {
            lastTrackedAnnouncementImpression = impression;
            void track('shown', 'exie-announcement', exieAnnouncementVersion, 'feature-announcement');
        }
    });

    export function openCatalog(source: ProductTourLaunchSource = 'catalog'): void {
        closeOverlays();
        requestErrorAvailability();
        catalogSource = source;
        catalogOpen = true;
    }

    export async function startTour<Name extends ProductTourName>(name: Name, source: ProductTourLaunchSource = 'catalog'): Promise<void> {
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
        const next = productTourCheckpoint.start(name, item.initialCheckpoint, source, currentUser.id, item.version, organizationId);
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
        if (!(await recordPreference('welcome', welcomeVersion, ProductTourStatus.Completed))) {
            return;
        }
        welcomeHandled = true;
        await track('completed', 'welcome', welcomeVersion, 'automatic');
        await startTour(recommended.name, 'automatic');
    }

    async function onWelcomeBrowse(): Promise<void> {
        if (!(await recordPreference('welcome', welcomeVersion, ProductTourStatus.Completed))) {
            return;
        }
        welcomeHandled = true;
        await track('completed', 'welcome', welcomeVersion, 'automatic');
        openCatalog('catalog');
    }

    async function onWelcomeSkip(): Promise<void> {
        if (!(await recordPreference('welcome', welcomeVersion, ProductTourStatus.Dismissed))) {
            return;
        }
        welcomeHandled = true;
        await track('dismissed', 'welcome', welcomeVersion, 'automatic');
    }

    async function onExieAnnouncementStart(): Promise<void> {
        if (!(await recordPreference('exie-announcement', exieAnnouncementVersion, ProductTourStatus.Completed))) {
            return;
        }
        await track('completed', 'exie-announcement', exieAnnouncementVersion, 'feature-announcement');
        await startTour('meet-exie', 'feature-announcement');
    }

    async function onExieAnnouncementDismiss(): Promise<void> {
        if (!(await recordPreference('exie-announcement', exieAnnouncementVersion, ProductTourStatus.Dismissed))) {
            return;
        }
        await track('dismissed', 'exie-announcement', exieAnnouncementVersion, 'feature-announcement');
    }

    function getItem<Name extends ProductTourName>(name: Name): ProductTourListItem<Name> {
        return items.find((item) => item.name === name)! as ProductTourListItem<Name>;
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
