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
    import { onMount, tick } from 'svelte';
    import { toast } from 'svelte-sonner';

    import type {
        ProductTourContext,
        ProductTourErrorEventAvailability,
        ProductTourId,
        ProductTourKey,
        ProductTourLaunchSource,
        ProductTourListItem,
        ProductTourStartAction,
        ProductTourStep
    } from '../types';

    import { PRODUCT_TOUR_ANCHORS, productTourSelector } from '../anchors';
    import { getProductTour, getProductTourItems, getRecommendedProductTourId } from '../catalog';
    import { shouldOfferProductTourAnnouncement, shouldOfferProductTourWelcome } from '../eligibility';
    import { clearProductTourSession, readProductTourSession, writeProductTourSession } from '../session';
    import { productTourHost, type ProductTourHostEvent } from '../state.svelte';
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
        requestErrorAvailability: () => void;
        routeKey: string;
        stateSettled: boolean;
    }

    const WELCOME_VERSION = 1;
    const EXIE_ANNOUNCEMENT_VERSION = 1;
    const EXIE_ANNOUNCEMENT_KEY = 'exie-announcement' as const;
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
        requestErrorAvailability,
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
    let pendingConfirmation = $state<{
        action: Extract<ProductTourStartAction, { type: 'confirm-navigation' }>;
        id: ProductTourId;
        source: ProductTourLaunchSource;
    }>();
    let driverInstance = $state.raw<Driver>();
    let driverTransition: 'active' | 'finishing' | 'inline' | 'navigation' = 'active';
    let driverRoute = '';
    let resumeAttemptedRoute = '';
    let overlayRevision = $state(0);

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
            !productTourHost.activeTourId &&
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
        observer.observe(document.body, {
            attributes: true,
            childList: true,
            subtree: true
        });
        return () => observer.disconnect();
    });

    $effect(() => {
        void overlayRevision;
        if (productTourHost.activeTourId !== 'investigate-error' || productTourHost.activeStepId !== 'choose-error') {
            return;
        }

        if (getVisibleEventType() === 'error') {
            onEventOpened('error');
        }
    });

    $effect(() => {
        const currentRoute = routeKey;
        if (!stateSettled || driverInstance || resumeAttemptedRoute === currentRoute) {
            return;
        }

        const stored = readProductTourSession();
        if (!stored) {
            return;
        }

        let storedVersion: number;
        try {
            storedVersion = getProductTour(stored.tourId).version;
        } catch {
            clearProductTourSession();
            return;
        }

        if (storedVersion !== stored.version) {
            clearProductTourSession();
            return;
        }

        resumeAttemptedRoute = currentRoute;
        if (stored.tourId === 'investigate-error' && stored.stepId === 'choose-error' && /\/(event|stack)\//.test(pathname)) {
            return;
        }

        void launch(stored.tourId, stored.source, stored.stepId, false);
    });

    $effect(() => {
        const currentRoute = routeKey;
        if (!driverInstance || !driverRoute || driverRoute === currentRoute) {
            return;
        }

        driverTransition = 'navigation';
        driverInstance.destroy();
        driverTransition = 'active';
        driverRoute = '';
        resumeAttemptedRoute = '';
    });

    $effect(() => {
        if (!stateSettled || currentUser) {
            return;
        }

        driverInstance?.destroy();
        driverInstance = undefined;
        productTourHost.clear();
        clearProductTourSession();
    });

    $effect(() => {
        const currentOrganizationId = organizationId;
        if (!productTourHost.activeTourId || productTourHost.organizationId === currentOrganizationId) {
            return;
        }

        if (productTourHost.activeTourId === 'configure-project' && pathname === resolve('/(app)/organization/add')) {
            productTourHost.set(productTourHost.activeTourId, productTourHost.activeStepId, productTourHost.source, currentOrganizationId);
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
        requestErrorAvailability();
        catalogSource = source;
        catalogOpen = true;
    }

    export async function startTour(id: ProductTourId, source: ProductTourLaunchSource = 'catalog'): Promise<void> {
        if (productTourHost.activeTourId) {
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
        const startAction = item.getStartAction?.({
            ...context,
            openEventType: getVisibleEventType()
        }) ?? {
            type: 'launch'
        };
        const canLaunchInsideOverlay = startAction.type === 'launch' && startAction.stepId === 'stack-summary';
        if (!(await waitForCompetingOverlaysToClose()) && !canLaunchInsideOverlay) {
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

        await executeStartAction(id, source, startAction);
    }

    async function executeStartAction(id: ProductTourId, source: ProductTourLaunchSource, action: ProductTourStartAction): Promise<void> {
        if (action.type === 'confirm-navigation') {
            pendingConfirmation = {
                action,
                id,
                source
            };
            return;
        }

        if (action.type === 'navigate' && action.destination !== routeKey) {
            writeProductTourSession({
                source,
                tourId: id,
                version: getProductTour(id).version
            });
            await goto(action.destination);
            return;
        }

        await launch(id, source, action.type === 'launch' ? action.stepId : undefined);
    }

    async function launch(id: ProductTourId, source: ProductTourLaunchSource, resumeStepId?: string, emitStarted = true): Promise<void> {
        const definition = getProductTour(id);
        if (id === 'new-ui-overview' && isMobileViewport()) {
            await ensureTarget({
                anchor: PRODUCT_TOUR_ANCHORS.appNavigation,
                description: '',
                id: 'mobile-navigation',
                title: ''
            });
        }

        const allSteps = await getAvailableSteps(id, definition.getSteps(context));
        let startIndex = resumeStepId ? allSteps.findIndex((step) => step.id === resumeStepId) : 0;
        if (startIndex < 0) {
            startIndex = 0;
        }

        const firstStep = allSteps[startIndex];
        if (!firstStep || !(await ensureTarget(firstStep))) {
            await failTour(id, definition.version, firstStep?.id ?? 'missing-start', source);
            return;
        }

        if (firstStep.presentation === 'inline') {
            productTourHost.set(id, firstStep.id, source, organizationId);
            writeProductTourSession({
                source,
                stepId: firstStep.id,
                tourId: id,
                version: definition.version
            });
            void track(emitStarted ? 'started' : 'step', id, definition.version, source, firstStep.id);
            return;
        }

        const { driver } = await import('driver.js');
        driverTransition = 'active';
        const steps = allSteps.slice(startIndex);

        driverInstance = driver({
            allowClose: true,
            animate: !window.matchMedia('(prefers-reduced-motion: reduce)').matches,
            disableActiveInteraction: false,
            onCloseClick: () => void dismissTour(id, definition.version, source),
            onDestroyed: () => {
                const activeStepId = productTourHost.activeStepId;
                const preservingNavigation = driverTransition === 'navigation' || (driverRoute !== '' && driverRoute !== routeKey);
                const preservingPendingEvent =
                    id === 'investigate-error' && (activeStepId === 'choose-error' || readProductTourSession()?.stepId === 'choose-error');
                driverInstance = undefined;
                if (driverTransition !== 'inline' && !preservingNavigation && !preservingPendingEvent) {
                    productTourHost.clear();
                    if (driverTransition === 'active') {
                        clearProductTourSession();
                        void track('dismissed', id, definition.version, source, activeStepId);
                    }
                }

                if (driverTransition === 'active' && !preservingNavigation && !preservingPendingEvent) {
                    void recordProgress(id, definition.version, 'dismissed').catch(() => toast.error('We could not save your guided-tour progress.'));
                }
            },
            onHighlighted: (_, __, options) => {
                const index = options.state.activeIndex ?? 0;
                const step = steps[index];
                if (!step) {
                    return;
                }

                productTourHost.set(id, step.id, source, organizationId);
                writeProductTourSession({
                    source,
                    stepId: step.resumeStepId ?? step.id,
                    tourId: id,
                    version: definition.version
                });
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
        productTourHost.set(id, firstStep.id, source, organizationId);
        writeProductTourSession({
            source,
            stepId: firstStep.resumeStepId ?? firstStep.id,
            tourId: id,
            version: definition.version
        });

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
                driverTransition = 'finishing';
                writeProductTourSession({
                    source,
                    stepId: step.resumeStepId,
                    tourId: id,
                    version
                });
                driverInstance?.destroy();
                driverInstance = undefined;
                productTourHost.clear();
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
            driverTransition = 'inline';
            writeProductTourSession({
                source,
                stepId: next.id,
                tourId: id,
                version
            });
            driverInstance?.destroy();
            driverTransition = 'active';
            productTourHost.set(id, next.id, source, organizationId);
            void track('step', id, version, source, next.id);
            return;
        }

        if (driverInstance && driverRoute === routeKey) {
            driverInstance.moveNext();
        } else {
            driverTransition = 'navigation';
            driverInstance?.destroy();
            driverInstance = undefined;
            driverTransition = 'active';
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
            observer.observe(document.body, {
                attributes: true,
                childList: true,
                subtree: true
            });
        });
    }

    function hasVisibleTarget(selector: string): boolean {
        const element = document.querySelector<HTMLElement>(selector);
        return !!element && element.getClientRects().length > 0;
    }

    function getVisibleEventType(): string | undefined {
        const element = document.querySelector<HTMLElement>(productTourSelector(PRODUCT_TOUR_ANCHORS.eventDetails));
        return element?.getClientRects().length ? element.dataset.eventType : undefined;
    }

    function isMobileViewport(): boolean {
        return window.matchMedia('(max-width: 767px)').matches;
    }

    async function getAvailableSteps(id: ProductTourId, steps: ProductTourStep[]): Promise<ProductTourStep[]> {
        const orderedSteps = orderStepsForViewport(id, steps);
        const availability = await Promise.all(orderedSteps.map((step) => (!step.optional || !step.anchor ? Promise.resolve(true) : ensureTarget(step))));

        return orderedSteps.filter((_, index) => availability[index]);
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

    async function confirmNavigation(): Promise<void> {
        const confirmation = pendingConfirmation;
        pendingConfirmation = undefined;
        if (confirmation) {
            await executeStartAction(confirmation.id, confirmation.source, {
                destination: confirmation.action.destination,
                type: 'navigate'
            });
        }
    }

    async function completeTour(id: ProductTourId, version: number, source: ProductTourLaunchSource): Promise<boolean> {
        driverTransition = 'finishing';
        try {
            await recordProgress(id, version, 'completed');
        } catch {
            driverTransition = 'active';
            toast.error('We could not save your guided-tour progress. Please try again.');
            return false;
        }

        clearProductTourSession();
        void track('completed', id, version, source);
        driverInstance?.destroy();
        productTourHost.clear();
        driverTransition = 'active';
        return true;
    }

    async function dismissTour(id: ProductTourId, version: number, source: ProductTourLaunchSource): Promise<void> {
        driverTransition = 'finishing';
        clearProductTourSession();
        await recordProgress(id, version, 'dismissed').catch(() => toast.error('We could not save your guided-tour progress.'));
        void track('dismissed', id, version, source, productTourHost.activeStepId);
        driverInstance?.destroy();
        productTourHost.clear();
        driverTransition = 'active';
    }

    async function failTour(id: ProductTourId, version: number, stepId: string, source: ProductTourLaunchSource): Promise<void> {
        driverTransition = 'finishing';
        clearProductTourSession();
        void track('failed', id, version, source, stepId);
        toast.error('This guide could not find the next screen. You can restart it from Guided Tours.');
        driverInstance?.destroy();
        productTourHost.clear();
        driverTransition = 'active';
    }

    function stopActiveTour(clearSession: boolean): void {
        driverTransition = 'finishing';
        if (clearSession) {
            clearProductTourSession();
        }

        driverInstance?.destroy();
        productTourHost.clear();
        driverTransition = 'active';
    }

    async function recordProgress(key: string, version: number, status: 'completed' | 'dismissed'): Promise<void> {
        await progressMutation.mutateAsync({
            progress: {
                status,
                version
            },
            tourId: key
        });
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

    async function onDomainComplete(id: ProductTourId): Promise<boolean> {
        if (productTourHost.activeTourId !== id || !productTourHost.source) {
            return true;
        }

        const definition = getProductTour(id);
        return completeTour(id, definition.version, productTourHost.source);
    }

    function onDomainDismiss(id: ProductTourId): void {
        if (productTourHost.activeTourId !== id || !productTourHost.source) {
            return;
        }

        const definition = getProductTour(id);
        void dismissTour(id, definition.version, productTourHost.source);
    }

    function onInlineAdvance(id: ProductTourId, stepId: string): void {
        if (productTourHost.activeTourId !== id || productTourHost.activeStepId !== stepId || !productTourHost.source) {
            return;
        }

        const definition = getProductTour(id);
        const steps = definition.getSteps(context).filter((step) => !step.optional || !step.anchor || hasVisibleTarget(productTourSelector(step.anchor)));
        const index = steps.findIndex((step) => step.id === stepId);
        if (index >= 0) {
            void advance(id, definition.version, productTourHost.source, steps, index);
        }
    }

    function onEventOpened(eventType?: string): void {
        const stored = readProductTourSession();
        const isActiveChooseError =
            productTourHost.activeTourId === 'investigate-error' && productTourHost.activeStepId === 'choose-error' && !!productTourHost.source;
        const isResumableChooseError = stored?.tourId === 'investigate-error' && stored.stepId === 'choose-error';
        if (eventType !== 'error' || (!isActiveChooseError && !isResumableChooseError)) {
            if (productTourHost.activeTourId === 'investigate-error' && eventType && eventType !== 'error') {
                toast.info('Choose an error event to continue this guide.');
            }

            return;
        }

        const definition = getProductTour('investigate-error');
        if (!isActiveChooseError && stored) {
            productTourHost.set('investigate-error', 'choose-error', stored.source, organizationId);
        }

        const source = productTourHost.source ?? stored?.source;
        if (!source) {
            return;
        }

        const steps = definition.getSteps(context).filter((step) => !step.optional || !step.anchor || hasVisibleTarget(productTourSelector(step.anchor)));
        const index = steps.findIndex((step) => step.id === 'choose-error');
        if (index >= 0) {
            productTourHost.set('investigate-error', 'stack-summary', source, organizationId);
            writeProductTourSession({
                source,
                stepId: 'stack-summary',
                tourId: 'investigate-error',
                version: definition.version
            });
            void advance('investigate-error', definition.version, source, steps, index);
        }
    }

    function onHostEvent(event: ProductTourHostEvent): boolean | Promise<boolean | void> | void {
        switch (event.type) {
            case 'advance':
                onInlineAdvance(event.tourId, event.stepId);
                break;
            case 'completed':
                return onDomainComplete(event.tourId);
            case 'dismissed':
                onDomainDismiss(event.tourId);
                break;
            case 'event-opened':
                onEventOpened(event.eventType);
                break;
        }
    }

    onMount(() => {
        const unsubscribe = productTourHost.subscribe(onHostEvent);
        return () => {
            unsubscribe();
            stopActiveTour(true);
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

{#if pendingConfirmation}
    <AlertDialog.Root
        open
        onOpenChange={(open) => {
            if (!open) {
                pendingConfirmation = undefined;
            }
        }}
    >
        <AlertDialog.Content data-product-tour-overlay>
            <AlertDialog.Header>
                <AlertDialog.Title>{pendingConfirmation.action.title}</AlertDialog.Title>
                <AlertDialog.Description>{pendingConfirmation.action.description}</AlertDialog.Description>
            </AlertDialog.Header>
            <AlertDialog.Footer>
                <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                <AlertDialog.Action onclick={() => void confirmNavigation()}>{pendingConfirmation.action.actionLabel}</AlertDialog.Action>
            </AlertDialog.Footer>
        </AlertDialog.Content>
    </AlertDialog.Root>
{/if}
