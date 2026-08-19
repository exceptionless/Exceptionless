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

    let { isModified, onDiscard, onSave, saving }: Props = $props();
    let isResumingNavigation = false;
    let pendingNavigation = $state<URL>();

    function stayOnPage(): void {
        pendingNavigation = undefined;
    }

    async function resumeNavigation(url: URL): Promise<void> {
        isResumingNavigation = true;
        pendingNavigation = undefined;
        try {
            await goto(url);
        } finally {
            isResumingNavigation = false;
        }
    }

    async function saveAndContinue(): Promise<void> {
        const url = pendingNavigation;
        if (!url || saving || !(await onSave())) {
            return;
        }

        await resumeNavigation(url);
    }

    function discardAndContinue(): void {
        const url = pendingNavigation;
        if (!url) {
            return;
        }

        onDiscard();
        void resumeNavigation(url);
    }

    beforeNavigate(({ cancel, to, willUnload }) => {
        if (!isModified || isResumingNavigation) {
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
        pendingNavigation = to.url;
    });
</script>

<SavedViewNavigationDialog open={pendingNavigation !== undefined} onDiscard={discardAndContinue} onSave={saveAndContinue} onStay={stayOnPage} {saving} />
