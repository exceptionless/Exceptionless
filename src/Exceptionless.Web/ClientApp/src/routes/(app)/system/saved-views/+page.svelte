<script lang="ts">
    import CopyToClipboardButton from '$comp/copy-to-clipboard-button.svelte';
    import { Button } from '$comp/ui/button';
    import { Spinner } from '$comp/ui/spinner';
    import * as Tabs from '$comp/ui/tabs';
    import { Textarea } from '$comp/ui/textarea';
    import { getOrgSavedViewsExportMutation, getPredefinedSavedViewsMutation, putPredefinedSavedViewsMutation } from '$features/admin/api.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import Save from '@lucide/svelte/icons/save';
    import { onMount } from 'svelte';
    import { toast } from 'svelte-sonner';

    let predefinedJson = $state('');
    let predefinedTab = $state('config');
    let toastId = $state<number | string>();

    const predefinedSavedViews = getPredefinedSavedViewsMutation();
    const organizationExport = getOrgSavedViewsExportMutation();
    const savePredefined = putPredefinedSavedViewsMutation();
    const organizationId = $derived(organization.current ?? '');

    function getErrorMessage(error: unknown, fallback: string) {
        return error instanceof ProblemDetails ? error.title : fallback;
    }

    async function loadPredefinedSavedViews() {
        toast.dismiss(toastId);

        try {
            predefinedJson = await predefinedSavedViews.mutateAsync();
        } catch (error: unknown) {
            toastId = toast.error(`An error occurred while loading predefined saved views: ${getErrorMessage(error, 'Please try again.')}`);
        }
    }

    async function loadOrganizationViews() {
        if (!organizationId) {
            return;
        }

        toast.dismiss(toastId);

        try {
            predefinedJson = await organizationExport.mutateAsync(organizationId);
        } catch (error: unknown) {
            toastId = toast.error(`An error occurred while loading organization views: ${getErrorMessage(error, 'Please try again.')}`);
        }
    }

    async function handleSavePredefined() {
        toast.dismiss(toastId);

        try {
            predefinedJson = await savePredefined.mutateAsync(predefinedJson);
            toastId = toast.success('Predefined saved views updated successfully.');
        } catch (error: unknown) {
            toastId = toast.error(`An error occurred while saving predefined saved views: ${getErrorMessage(error, 'Please try again.')}`);
        }
    }

    function handleTabChange(value: string) {
        predefinedTab = value;
        if (value === 'config') {
            void loadPredefinedSavedViews();
        } else {
            void loadOrganizationViews();
        }
    }

    onMount(() => {
        void loadPredefinedSavedViews();
    });
</script>

<div class="space-y-4">
    <div class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <Tabs.Root value={predefinedTab} onValueChange={handleTabChange}>
            <Tabs.List>
                <Tabs.Trigger value="config">Predefined</Tabs.Trigger>
                <Tabs.Trigger value="org">Current Org</Tabs.Trigger>
            </Tabs.List>
            <Tabs.Content value="config" class="hidden"></Tabs.Content>
            <Tabs.Content value="org" class="hidden"></Tabs.Content>
        </Tabs.Root>

        <CopyToClipboardButton value={predefinedJson} variant="outline" />
    </div>

    <Textarea bind:value={predefinedJson} class="font-mono text-xs max-h-[60vh] min-h-96 overflow-auto" rows={24} spellcheck={false} />

    <div class="flex justify-end">
        <Button
            disabled={savePredefined.isPending || !predefinedJson.trim()}
            onclick={() => {
                void handleSavePredefined();
            }}
        >
            {#if savePredefined.isPending}
                <Spinner />
                Saving...
            {:else}
                <Save class="size-4" aria-hidden="true" />
                Save Predefined
            {/if}
        </Button>
    </div>
</div>
