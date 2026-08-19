<script lang="ts">
    import { beforeNavigate, goto } from '$app/navigation';
    import { page } from '$app/state';

    import SavedViewNavigationDialog from './saved-view-navigation-dialog.svelte';

    interface Props {
        isModified: boolean;
        onDiscard: () => void;
        onSave: () => Promise<boolean>;
        saving: boolean;
    }

    interface PendingNavigation {
        delta: number | undefined;
        url: URL;
    }

    let { isModified, onDiscard, onSave, saving }: Props = $props();
    let isResumingNavigation = false;
    let pendingNavigation = $state<PendingNavigation>();

    function stayOnPage(): void {
        pendingNavigation = undefined;
    }

    async function resumeNavigation(navigation: PendingNavigation): Promise<void> {
        isResumingNavigation = true;
        pendingNavigation = undefined;

        if (navigation.delta !== undefined) {
            history.go(navigation.delta);
            return;
        }

        try {
            await goto(navigation.url);
        } catch (error) {
            isResumingNavigation = false;
            throw error;
        }
    }

    async function saveAndContinue(): Promise<void> {
        const navigation = pendingNavigation;
        if (!navigation || saving || !(await onSave())) {
            return;
        }

        await resumeNavigation(navigation);
    }

    function discardAndContinue(): void {
        const navigation = pendingNavigation;
        if (!navigation) {
            return;
        }

        onDiscard();
        void resumeNavigation(navigation);
    }

    beforeNavigate(({ cancel, delta, to, type, willUnload }) => {
        if (isResumingNavigation) {
            isResumingNavigation = false;
            return;
        }

        if (!isModified) {
            return;
        }

        if (willUnload) {
            cancel();
            return;
        }

        if (!to || (to.url.pathname === page.url.pathname && to.url.searchParams.get('saved') === page.url.searchParams.get('saved'))) {
            return;
        }

        cancel();
        pendingNavigation = {
            delta: type === 'popstate' ? delta : undefined,
            url: to.url
        };
    });
</script>

<SavedViewNavigationDialog open={pendingNavigation !== undefined} onDiscard={discardAndContinue} onSave={saveAndContinue} onStay={stayOnPage} {saving} />
