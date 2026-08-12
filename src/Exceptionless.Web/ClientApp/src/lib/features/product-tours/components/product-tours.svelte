<script lang="ts">
    import type { AssistantAccess } from '$features/assistant/models';
    import type { ViewProject } from '$features/projects/models';
    import type { ViewCurrentUser } from '$features/users/models';
    import type { Driver } from 'driver.js';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { submitFeatureUsage } from '$features/auth/exceptionless-session';
    import { putCurrentUserProductTour } from '$features/users/api.svelte';
    import { tick } from 'svelte';
    import { toast } from 'svelte-sonner';

    import type {
        ProductTourContext,
        ProductTourErrorEventAvailability,
        ProductTourId,
        ProductTourKey,
        ProductTourLaunchSource,
        ProductTourListItem,
        ProductTourStep
    } from '../types';

    import { PRODUCT_TOUR_ANCHORS, productTourSelector } from '../anchors';
    import { getProductTour, getProductTourItems, getRecommendedProductTourId } from '../catalog';
    import { shouldOfferProductTourAnnouncement, shouldOfferProductTourWelcome } from '../eligibility';
    import { productTourRuntime } from '../state.svelte';
    import { buildProductTourTelemetryEvent, type ProductTourTelemetryEvent } from '../telemetry';
    import ProductTourCatalog from './product-tour-catalog.svelte';
    import ProductTourFeatureAnnouncement from './product-tour-feature-announcement.svelte';
    import ProductTourWelcome from './product-tour-welcome.svelte';
    import 'driver.js/dist/driver.css';

    import '../product-tours.css';

    interface Props {
        assistantAccess?: AssistantAccess;
        closeOverlays: () => void;
        currentUser?: ViewCurrentUser;
        errorEventAvailability: ProductTourErrorEventAvailability;
        isAnyOverlayOpen: boolean;
        isImpersonating: boolean;
        isSetupPage: boolean;
        organizationId?: string;
        pathname: string;
        projects: ViewProject[];
        routeKey: string;
        stateSettled: boolean;
    }

    interface StoredTourState {
        source: ProductTourLaunchSource;
        stepId?: string;
        tourId: ProductTourId;
        version: number;
    }

    const WELCOME_VERSION = 1;
    const EXIE_ANNOUNCEMENT_VERSION = 1;
    const EXIE_ANNOUNCEMENT_KEY = 'exie-announcement' as const;
    const SESSION_KEY = 'exceptionless.product-tour';
    const SYSTEM_PATH = resolve('/(app)/system');

    let {
        assistantAccess,
        closeOverlays,
        currentUser,
        errorEventAvailability,
        isAnyOverlayOpen,
        isImpersonating,
        isSetupPage,
        organizationId,
        pathname,
        projects,
        routeKey,
        stateSettled
    }: Props = $props();

    let catalogOpen = $state(false);
    let catalogSource = $state<ProductTourLaunchSource>('catalog');
    let welcomeOpen = $state(false);
    let welcomeHandled = $state(false);
    let welcomeShown = $state(false);
    let welcomeBrowsePending = $state(false);
    let exieAnnouncementOpen = $state(false);
    let exieAnnouncementShown = $state(false);
    let confirmNewProjectOpen = $state(false);
    let pendingConfigureSource = $state<ProductTourLaunchSource>();
    let confirmErrorNavigationOpen = $state(false);
    let pendingInvestigateSource = $state<ProductTourLaunchSource>();
    let driverInstance = $state.raw<Driver>();
    let activeSource = $state<ProductTourLaunchSource>();
    let activeOrganizationId = $state<string>();
    let isFinishing = false;
    let isSuspendingInline = false;
    let isNavigatingTour = false;
    let driverRoute = '';
    let resumeAttemptedRoute = '';
    let overlayRevision = $state(0);

    const progressMutation = putCurrentUserProductTour();
    const context = $derived<ProductTourContext>({ assistantAccess, errorEventAvailability, isSetupPage, organizationId, pathname, projects });
    const items = $derived(getProductTourItems(context, currentUser?.product_tours));
    const recommended = $derived.by(() => {
        const id = getRecommendedProductTourId(context);
        return items.find((item) => item.id === id) ?? items[0]!;
    });

    $effect(() => {
        const welcomeProgress = currentUser?.product_tours?.welcome;
        void overlayRevision;
        const shouldShow =
            stateSettled &&
            !!currentUser &&
            !welcomeHandled &&
            !welcomeOpen &&
            !catalogOpen &&
            !isAnyOverlayOpen &&
            !hasCompetingOverlay() &&
            !isImpersonating &&
            !pathname.startsWith(SYSTEM_PATH) &&
            shouldOfferProductTourWelcome(welcomeProgress, WELCOME_VERSION);

        if (!shouldShow) {
            return;
        }

        welcomeOpen = true;
        if (!welcomeShown) {
            welcomeShown = true;
            void track('chooser-shown', 'welcome', WELCOME_VERSION, 'automatic');
        }
    });

    $effect(() => {
        const welcomeProgress = currentUser?.product_tours?.welcome;
        const announcementProgress = currentUser?.product_tours?.[EXIE_ANNOUNCEMENT_KEY];
        const isMeaningfulAppRoute = pathname.startsWith('/next/event') || pathname.startsWith('/next/stack');
        const shouldShow =
            stateSettled &&
            !!currentUser &&
            !!assistantAccess?.enabled &&
            isMeaningfulAppRoute &&
            !isSetupPage &&
            !isImpersonating &&
            !productTourRuntime.activeTourId &&
            !exieAnnouncementShown &&
            !exieAnnouncementOpen &&
            !welcomeOpen &&
            !catalogOpen &&
            !isAnyOverlayOpen &&
            !hasCompetingOverlay() &&
            !pathname.startsWith(SYSTEM_PATH) &&
            !shouldOfferProductTourWelcome(welcomeProgress, WELCOME_VERSION) &&
            shouldOfferProductTourAnnouncement(announcementProgress, EXIE_ANNOUNCEMENT_VERSION);

        if (!shouldShow) {
            return;
        }

        exieAnnouncementOpen = true;
        exieAnnouncementShown = true;
        void track('announcement-shown', EXIE_ANNOUNCEMENT_KEY, EXIE_ANNOUNCEMENT_VERSION, 'feature-announcement');
    });

    $effect(() => {
        const observer = new MutationObserver(() => (overlayRevision += 1));
        observer.observe(document.body, { childList: true });
        return () => observer.disconnect();
    });

    $effect(() => {
        const currentRoute = routeKey;
        if (!stateSettled || driverInstance || resumeAttemptedRoute === currentRoute) {
            return;
        }

        const stored = readStoredState();
        if (!stored) {
            return;
        }

        let storedVersion: number;
        try {
            storedVersion = getProductTour(stored.tourId).version;
        } catch {
            clearStoredState();
            return;
        }

        if (storedVersion !== stored.version) {
            clearStoredState();
            return;
        }

        resumeAttemptedRoute = currentRoute;
        void launch(stored.tourId, stored.source, stored.stepId, false);
    });

    $effect(() => {
        const currentRoute = routeKey;
        if (!driverInstance || !driverRoute || driverRoute === currentRoute) {
            return;
        }

        isNavigatingTour = true;
        isFinishing = true;
        driverInstance.destroy();
        isNavigatingTour = false;
        isFinishing = false;
        driverRoute = '';
        resumeAttemptedRoute = '';
    });

    $effect(() => {
        if (!stateSettled || currentUser) {
            return;
        }

        driverInstance?.destroy();
        driverInstance = undefined;
        productTourRuntime.clear();
        clearStoredState();
    });

    $effect(() => {
        const currentOrganizationId = organizationId;
        if (!productTourRuntime.activeTourId || activeOrganizationId === currentOrganizationId) {
            return;
        }

        if (productTourRuntime.activeTourId === 'configure-project' && pathname === resolve('/(app)/organization/add')) {
            activeOrganizationId = currentOrganizationId;
            return;
        }

        stopActiveTour(true);
    });

    function getItem(id: ProductTourId): ProductTourListItem {
        return items.find((item) => item.id === id)!;
    }

    function hasCompetingOverlay(): boolean {
        return Array.from(document.querySelectorAll<HTMLElement>('[role="alertdialog"], [role="dialog"], [data-assistant-panel]')).some(
            (element) => element.getClientRects().length > 0 && !element.closest('.driver-popover') && !element.closest('[data-product-tour-overlay]')
        );
    }

    async function waitForCompetingOverlaysToClose(timeout = 1000): Promise<boolean> {
        await tick();
        const deadline = performance.now() + timeout;
        while (hasCompetingOverlay() && performance.now() < deadline) {
            await new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));
        }

        return !hasCompetingOverlay();
    }

    export function openCatalog(source: ProductTourLaunchSource = 'catalog'): void {
        closeOverlays();
        catalogSource = source;
        catalogOpen = true;
    }

    export async function startTour(id: ProductTourId, source: ProductTourLaunchSource = 'catalog'): Promise<void> {
        if (productTourRuntime.activeTourId) {
            stopActiveTour(true);
        }

        const item = getItem(id);
        if (!item.availability.available) {
            openCatalog(source);
            return;
        }

        catalogOpen = false;
        welcomeOpen = false;
        exieAnnouncementOpen = false;
        closeOverlays();
        const canReuseOpenError = id === 'investigate-error' && hasVisibleTarget(productTourSelector(PRODUCT_TOUR_ANCHORS.eventDetails));
        if (!(await waitForCompetingOverlaysToClose()) && !canReuseOpenError) {
            toast.info('Close the open dialog or panel before starting a guided tour.');
            return;
        }

        if (welcomeBrowsePending) {
            try {
                await recordProgress('welcome', WELCOME_VERSION, 'completed');
            } catch {
                toast.error('We could not save your guided-tour preference. Please try again.');
                return;
            }

            welcomeBrowsePending = false;
            void track('chooser-started', id, item.version, source);
        }

        if (id === 'configure-project' && organizationId && !projects.some((project) => !project.is_configured) && !pathname.includes('/project/add')) {
            pendingConfigureSource = source;
            confirmNewProjectOpen = true;
            return;
        }

        if (id === 'investigate-error') {
            if (canReuseOpenError) {
                await launch(id, source, 'inspect-details');
                return;
            }

            if (pathname !== resolve('/(app)/event')) {
                pendingInvestigateSource = source;
                confirmErrorNavigationOpen = true;
                return;
            }
        }

        await navigateOrLaunch(id, source);
    }

    async function confirmErrorNavigation(): Promise<void> {
        const source = pendingInvestigateSource;
        pendingInvestigateSource = undefined;
        confirmErrorNavigationOpen = false;
        if (source) {
            await navigateOrLaunch('investigate-error', source);
        }
    }

    async function navigateOrLaunch(id: ProductTourId, source: ProductTourLaunchSource): Promise<void> {
        const destination = getDestination(id);
        const definition = getProductTour(id);

        if (destination && destination !== routeKey) {
            writeStoredState({ source, tourId: id, version: definition.version });
            await goto(destination);
            return;
        }

        await launch(id, source);
    }

    function getDestination(id: ProductTourId): string | undefined {
        if (id === 'create-saved-view') {
            return resolve('/(app)/event');
        }

        if (id === 'investigate-error') {
            return `${resolve('/(app)/event')}?time=all&type=error`;
        }

        if (id !== 'configure-project') {
            return undefined;
        }

        if (!organizationId) {
            return resolve('/(app)/organization/add');
        }

        const project = projects.find((item) => !item.is_configured);
        return project?.id ? `${resolve('/(app)/project/[projectId]/configure', { projectId: project.id })}?redirect=true` : undefined;
    }

    async function launch(id: ProductTourId, source: ProductTourLaunchSource, resumeStepId?: string, emitStarted = true): Promise<void> {
        const definition = getProductTour(id);
        if (id === 'new-ui-overview' && isMobileViewport()) {
            await ensureTarget({ anchor: PRODUCT_TOUR_ANCHORS.appNavigation, description: '', id: 'mobile-navigation', title: '' });
        }

        const allSteps = orderStepsForViewport(id, definition.getSteps(context)).filter(
            (step) => !step.optional || !step.anchor || hasVisibleTarget(productTourSelector(step.anchor))
        );
        const detailRouteResume = id === 'investigate-error' && resumeStepId === 'choose-error' && /\/(event|stack)\//.test(pathname);
        const effectiveResumeStepId = detailRouteResume ? 'inspect-details' : resumeStepId;
        let startIndex = effectiveResumeStepId ? allSteps.findIndex((step) => step.id === effectiveResumeStepId) : 0;
        if (startIndex < 0) {
            startIndex = 0;
        }

        const firstStep = allSteps[startIndex];
        if (!firstStep || !(await ensureTarget(firstStep))) {
            await failTour(id, definition.version, firstStep?.id ?? 'missing-start', source);
            return;
        }

        if (firstStep.presentation === 'inline') {
            activeSource = source;
            activeOrganizationId = organizationId;
            productTourRuntime.set(id, firstStep.id);
            writeStoredState({ source, stepId: firstStep.id, tourId: id, version: definition.version });
            void track(emitStarted ? 'started' : 'step', id, definition.version, source, firstStep.id);
            return;
        }

        const { driver } = await import('driver.js');
        activeSource = source;
        activeOrganizationId = organizationId;
        isFinishing = false;
        const steps = allSteps.slice(startIndex);

        driverInstance = driver({
            allowClose: true,
            animate: !window.matchMedia('(prefers-reduced-motion: reduce)').matches,
            disableActiveInteraction: false,
            onCloseClick: () => void dismissTour(id, definition.version, source),
            onDestroyed: () => {
                const activeStepId = productTourRuntime.activeStepId;
                const preservingNavigation = isNavigatingTour || (driverRoute !== '' && driverRoute !== routeKey);
                const preservingPendingEvent = id === 'investigate-error' && (activeStepId === 'choose-error' || readStoredState()?.stepId === 'choose-error');
                driverInstance = undefined;
                if (!isSuspendingInline && !preservingNavigation && !preservingPendingEvent) {
                    productTourRuntime.clear();
                    activeSource = undefined;
                    activeOrganizationId = undefined;
                    if (!isFinishing) {
                        clearStoredState();
                        void track('dismissed', id, definition.version, source, activeStepId);
                    }
                }

                if (!isFinishing && !isSuspendingInline && !preservingNavigation && !preservingPendingEvent) {
                    void recordProgress(id, definition.version, 'dismissed').catch(() => toast.error('We could not save your guided-tour progress.'));
                }
            },
            onHighlighted: (_, __, options) => {
                const index = options.state.activeIndex ?? 0;
                const step = steps[index];
                if (!step) {
                    return;
                }

                productTourRuntime.set(id, step.id);
                writeStoredState({ source, stepId: step.resumeStepId ?? step.id, tourId: id, version: definition.version });
                void track('step', id, definition.version, source, step.id);
            },
            overlayClickBehavior: 'close',
            popoverClass: 'product-tour-popover',
            showProgress: true,
            smoothScroll: true,
            steps: steps.map((step, index) => ({
                element: step.anchor ? () => document.querySelector(productTourSelector(step.anchor!))! : undefined,
                popover: {
                    description: step.description,
                    doneBtnText: 'Done',
                    nextBtnText: step.advanceOnClick ? 'Continue' : 'Next',
                    onNextClick: () => void advance(id, definition.version, source, steps, index),
                    showButtons: getButtons(step),
                    title: step.title
                }
            }))
        });
        driverRoute = routeKey;
        productTourRuntime.set(id, firstStep.id);
        writeStoredState({ source, stepId: firstStep.resumeStepId ?? firstStep.id, tourId: id, version: definition.version });

        if (emitStarted) {
            void track('started', id, definition.version, source);
        }

        driverInstance.drive(0);
    }

    function getButtons(step: ProductTourStep): Array<'close' | 'next' | 'previous'> {
        const buttons: Array<'close' | 'next' | 'previous'> = ['close'];

        if (step.showDone !== false) {
            buttons.push('next');
        }

        return buttons;
    }

    async function advance(id: ProductTourId, version: number, source: ProductTourLaunchSource, steps: ProductTourStep[], index: number): Promise<void> {
        const step = steps[index]!;
        if (id === 'configure-project' && step.id === 'choose-platform') {
            const platform = document.querySelector(productTourSelector(PRODUCT_TOUR_ANCHORS.projectConfigurePlatform));
            if (!platform || /Please select a project type/i.test(platform.textContent ?? '')) {
                toast.info('Choose the SDK platform before continuing.');
                return;
            }
        }

        const previousRoute = routeKey;
        if (step.advanceOnClick && step.anchor) {
            (document.querySelector(productTourSelector(step.anchor)) as HTMLElement | null)?.click();
            if (id === 'configure-project' && step.resumeStepId) {
                if (!(await waitForRouteChange(previousRoute))) {
                    toast.info('Finish the required fields before continuing this guide.');
                    return;
                }
            } else {
                await tick();
            }
        }

        const next = steps[index + 1];
        if (!next) {
            if (step.advanceOnClick && step.resumeStepId) {
                isFinishing = true;
                writeStoredState({ source, stepId: step.resumeStepId, tourId: id, version });
                driverInstance?.destroy();
                driverInstance = undefined;
                productTourRuntime.clear();
                activeSource = undefined;
                activeOrganizationId = undefined;
                return;
            }

            await completeTour(id, version, source);
            return;
        }

        if (id === 'new-ui-overview' && isMobileViewport() && next.id !== 'help' && next.id !== 'saved-views' && next.id !== 'navigation') {
            closeMobileNavigation();
            await tick();
        }

        if (!(await ensureTarget(next))) {
            if (next.optional) {
                driverInstance?.moveNext();
                return;
            }

            await failTour(id, version, next.id, source);
            return;
        }

        if (next.presentation === 'inline') {
            isSuspendingInline = true;
            isFinishing = true;
            writeStoredState({ source, stepId: next.id, tourId: id, version });
            driverInstance?.destroy();
            isSuspendingInline = false;
            isFinishing = false;
            activeSource = source;
            activeOrganizationId = organizationId;
            productTourRuntime.set(id, next.id);
            void track('step', id, version, source, next.id);
            return;
        }

        if (driverInstance && driverRoute === routeKey) {
            driverInstance.moveNext();
        } else {
            isNavigatingTour = true;
            isFinishing = true;
            driverInstance?.destroy();
            driverInstance = undefined;
            isNavigatingTour = false;
            isFinishing = false;
            await launch(id, source, next.id, false);
        }
    }

    async function ensureTarget(step: ProductTourStep): Promise<boolean> {
        if (!step.anchor) {
            return true;
        }

        const selector = productTourSelector(step.anchor);
        if (step.anchor === PRODUCT_TOUR_ANCHORS.appNavigation && isMobileViewport() && !hasVisibleTarget(selector)) {
            (document.querySelector(productTourSelector('mobile-navigation-trigger')) as HTMLElement | null)?.click();
        }

        if (hasVisibleTarget(selector)) {
            return true;
        }

        const timeout = step.waitForElement ?? 1200;
        return await new Promise((resolvePromise) => {
            const observer = new MutationObserver(() => {
                if (!hasVisibleTarget(selector)) {
                    return;
                }

                observer.disconnect();
                window.clearTimeout(timeoutId);
                resolvePromise(true);
            });
            const timeoutId = window.setTimeout(() => {
                observer.disconnect();
                resolvePromise(false);
            }, timeout);
            observer.observe(document.body, { attributes: true, childList: true, subtree: true });
        });
    }

    function hasVisibleTarget(selector: string): boolean {
        const element = document.querySelector<HTMLElement>(selector);
        return !!element && element.getClientRects().length > 0;
    }

    function isMobileViewport(): boolean {
        return window.matchMedia('(max-width: 767px)').matches;
    }

    function orderStepsForViewport(id: ProductTourId, steps: ProductTourStep[]): ProductTourStep[] {
        if (id !== 'new-ui-overview' || !isMobileViewport()) {
            return steps;
        }

        const order = ['navigation', 'saved-views', 'help', 'command-search', 'exie'];
        return order.map((stepId) => steps.find((step) => step.id === stepId)).filter((step): step is ProductTourStep => !!step);
    }

    function closeMobileNavigation(): void {
        if (hasVisibleTarget(productTourSelector(PRODUCT_TOUR_ANCHORS.appNavigation))) {
            (document.querySelector(productTourSelector('mobile-navigation-trigger')) as HTMLElement | null)?.click();
        }
    }

    async function waitForRouteChange(previousRoute: string, timeout = 10000): Promise<boolean> {
        const deadline = performance.now() + timeout;
        while (routeKey === previousRoute && `${window.location.pathname}${window.location.search}` === previousRoute && performance.now() < deadline) {
            await new Promise<void>((resolvePromise) => setTimeout(resolvePromise, 50));
        }

        return routeKey !== previousRoute || `${window.location.pathname}${window.location.search}` !== previousRoute;
    }

    async function onWelcomeStart(): Promise<void> {
        try {
            await recordProgress('welcome', WELCOME_VERSION, 'completed');
        } catch {
            toast.error('We could not save your guided-tour preference. Please try again.');
            return;
        }

        welcomeHandled = true;
        welcomeOpen = false;
        void track('chooser-started', recommended.id, recommended.version, 'automatic');
        await startTour(recommended.id, 'automatic');
    }

    async function onExieAnnouncementStart(): Promise<void> {
        try {
            await recordProgress(EXIE_ANNOUNCEMENT_KEY, EXIE_ANNOUNCEMENT_VERSION, 'completed');
        } catch {
            toast.error('We could not save the Exie announcement preference. Please try again.');
            return;
        }

        exieAnnouncementOpen = false;
        void track('announcement-started', EXIE_ANNOUNCEMENT_KEY, EXIE_ANNOUNCEMENT_VERSION, 'feature-announcement');
        await startTour('meet-exie', 'feature-announcement');
    }

    async function onExieAnnouncementDismiss(): Promise<void> {
        try {
            await recordProgress(EXIE_ANNOUNCEMENT_KEY, EXIE_ANNOUNCEMENT_VERSION, 'dismissed');
        } catch {
            toast.error('We could not save the Exie announcement preference. Please try again.');
            return;
        }

        exieAnnouncementOpen = false;
        void track('announcement-dismissed', EXIE_ANNOUNCEMENT_KEY, EXIE_ANNOUNCEMENT_VERSION, 'feature-announcement');
    }

    async function onWelcomeSkip(): Promise<void> {
        try {
            await recordProgress('welcome', WELCOME_VERSION, 'dismissed');
        } catch {
            toast.error('We could not save your guided-tour preference. Please try again.');
            return;
        }

        welcomeHandled = true;
        welcomeOpen = false;
        void track('chooser-skipped', 'welcome', WELCOME_VERSION, 'automatic');
    }

    function onWelcomeBrowse(): void {
        welcomeHandled = true;
        welcomeBrowsePending = true;
        welcomeOpen = false;
        openCatalog('catalog');
    }

    async function confirmNewProject(): Promise<void> {
        const source = pendingConfigureSource ?? 'catalog';
        pendingConfigureSource = undefined;
        confirmNewProjectOpen = false;
        const definition = getProductTour('configure-project');
        writeStoredState({ source, tourId: definition.id, version: definition.version });
        await goto(resolve('/(app)/project/add'));
    }

    async function completeTour(id: ProductTourId, version: number, source: ProductTourLaunchSource): Promise<void> {
        isFinishing = true;
        try {
            await recordProgress(id, version, 'completed');
        } catch {
            isFinishing = false;
            toast.error('We could not save your guided-tour progress. Please try again.');
            return;
        }

        clearStoredState();
        void track('completed', id, version, source);
        driverInstance?.destroy();
        productTourRuntime.clear();
        activeSource = undefined;
        activeOrganizationId = undefined;
    }

    async function dismissTour(id: ProductTourId, version: number, source: ProductTourLaunchSource): Promise<void> {
        isFinishing = true;
        clearStoredState();
        await recordProgress(id, version, 'dismissed').catch(() => toast.error('We could not save your guided-tour progress.'));
        void track('dismissed', id, version, source, productTourRuntime.activeStepId);
        driverInstance?.destroy();
        productTourRuntime.clear();
        activeSource = undefined;
        activeOrganizationId = undefined;
    }

    async function failTour(id: ProductTourId, version: number, stepId: string, source: ProductTourLaunchSource): Promise<void> {
        isFinishing = true;
        clearStoredState();
        void track('failed', id, version, source, stepId);
        toast.error('This guide could not find the next screen. You can restart it from Guided Tours.');
        driverInstance?.destroy();
        productTourRuntime.clear();
        activeSource = undefined;
        activeOrganizationId = undefined;
    }

    function stopActiveTour(clearSession: boolean): void {
        isFinishing = true;
        if (clearSession) {
            clearStoredState();
        }

        driverInstance?.destroy();
        productTourRuntime.clear();
        activeSource = undefined;
        activeOrganizationId = undefined;
    }

    async function recordProgress(key: string, version: number, status: 'completed' | 'dismissed'): Promise<void> {
        await progressMutation.mutateAsync({ progress: { status, version }, tourId: key });
    }

    async function track(
        event: ProductTourTelemetryEvent,
        id: ProductTourKey,
        version: number,
        source: ProductTourLaunchSource,
        stepId?: string
    ): Promise<void> {
        await submitFeatureUsage(buildProductTourTelemetryEvent(event, id, version, source, stepId)).catch(() => undefined);
    }

    function readStoredState(): StoredTourState | undefined {
        try {
            const value = sessionStorage.getItem(SESSION_KEY);
            return value ? (JSON.parse(value) as StoredTourState) : undefined;
        } catch {
            clearStoredState();
            return undefined;
        }
    }

    function writeStoredState(state: StoredTourState): void {
        sessionStorage.setItem(SESSION_KEY, JSON.stringify(state));
    }

    function clearStoredState(): void {
        sessionStorage.removeItem(SESSION_KEY);
    }

    function onDomainComplete(event: Event): void {
        const detail = (event as CustomEvent<{ tourId?: ProductTourId }>).detail;
        const id = detail?.tourId;
        if (!id || productTourRuntime.activeTourId !== id || !activeSource) {
            return;
        }

        const definition = getProductTour(id);
        void completeTour(id, definition.version, activeSource);
    }

    function onDomainDismiss(event: Event): void {
        const id = (event as CustomEvent<{ tourId?: ProductTourId }>).detail?.tourId;
        if (!id || productTourRuntime.activeTourId !== id || !activeSource) {
            return;
        }

        const definition = getProductTour(id);
        void dismissTour(id, definition.version, activeSource);
    }

    function onInlineAdvance(event: Event): void {
        const detail = (event as CustomEvent<{ stepId?: string; tourId?: ProductTourId }>).detail;
        const id = detail?.tourId;
        const stepId = detail?.stepId;
        if (!id || !stepId || productTourRuntime.activeTourId !== id || productTourRuntime.activeStepId !== stepId || !activeSource) {
            return;
        }

        const definition = getProductTour(id);
        const steps = definition.getSteps(context).filter((step) => !step.optional || !step.anchor || hasVisibleTarget(productTourSelector(step.anchor)));
        const index = steps.findIndex((step) => step.id === stepId);
        if (index >= 0) {
            void advance(id, definition.version, activeSource, steps, index);
        }
    }

    function onEventOpened(event: Event): void {
        const eventType = (event as CustomEvent<{ eventType?: string }>).detail?.eventType;
        const stored = readStoredState();
        const isActiveChooseError =
            productTourRuntime.activeTourId === 'investigate-error' && productTourRuntime.activeStepId === 'choose-error' && !!activeSource;
        const isResumableChooseError = stored?.tourId === 'investigate-error' && stored.stepId === 'choose-error';
        if (eventType !== 'error' || (!isActiveChooseError && !isResumableChooseError)) {
            if (productTourRuntime.activeTourId === 'investigate-error' && eventType && eventType !== 'error') {
                toast.info('Choose an error event to continue this guide.');
            }

            return;
        }

        const definition = getProductTour('investigate-error');
        if (!isActiveChooseError && stored) {
            activeSource = stored.source;
            activeOrganizationId = organizationId;
            productTourRuntime.set('investigate-error', 'choose-error');
        }

        const source = activeSource ?? stored?.source;
        if (!source) {
            return;
        }

        const steps = definition.getSteps(context).filter((step) => !step.optional || !step.anchor || hasVisibleTarget(productTourSelector(step.anchor)));
        const index = steps.findIndex((step) => step.id === 'choose-error');
        if (index >= 0) {
            productTourRuntime.set('investigate-error', 'inspect-details');
            writeStoredState({ source, stepId: 'inspect-details', tourId: 'investigate-error', version: definition.version });
            void advance('investigate-error', definition.version, source, steps, index);
        }
    }

    $effect(() => {
        document.addEventListener('product-tour:completed', onDomainComplete);
        document.addEventListener('product-tour:dismissed', onDomainDismiss);
        document.addEventListener('product-tour:advance', onInlineAdvance);
        document.addEventListener('product-tour:event-opened', onEventOpened);
        return () => {
            document.removeEventListener('product-tour:completed', onDomainComplete);
            document.removeEventListener('product-tour:dismissed', onDomainDismiss);
            document.removeEventListener('product-tour:advance', onInlineAdvance);
            document.removeEventListener('product-tour:event-opened', onEventOpened);
        };
    });
</script>

<ProductTourWelcome
    bind:open={welcomeOpen}
    onBrowse={onWelcomeBrowse}
    onSkip={() => void onWelcomeSkip()}
    onStart={() => void onWelcomeStart()}
    {recommended}
/>

{#if exieAnnouncementOpen && assistantAccess}
    <ProductTourFeatureAnnouncement
        hasAccess={assistantAccess.has_access}
        message={assistantAccess.message}
        onDismiss={() => void onExieAnnouncementDismiss()}
        onStart={() => void onExieAnnouncementStart()}
    />
{/if}

<ProductTourCatalog bind:open={catalogOpen} {items} onStart={(id) => void startTour(id, catalogSource)} />

<AlertDialog.Root bind:open={confirmNewProjectOpen}>
    <AlertDialog.Content data-product-tour-overlay>
        <AlertDialog.Header>
            <AlertDialog.Title>Create another project?</AlertDialog.Title>
            <AlertDialog.Description>
                Every accessible project is already configured. A new project uses plan capacity and will remain after the guide.
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel onclick={() => (pendingConfigureSource = undefined)}>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action onclick={() => void confirmNewProject()}>Create Project</AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>

<AlertDialog.Root bind:open={confirmErrorNavigationOpen}>
    <AlertDialog.Content data-product-tour-overlay>
        <AlertDialog.Header>
            <AlertDialog.Title>Open Errors?</AlertDialog.Title>
            <AlertDialog.Description>This guide starts in Errors so you can choose a real report. Your current page will change.</AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel onclick={() => (pendingInvestigateSource = undefined)}>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action onclick={() => void confirmErrorNavigation()}>Open Errors</AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
