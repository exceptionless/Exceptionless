<script lang="ts">
    import CopyToClipboardButton from '$comp/copy-to-clipboard-button.svelte';
    import { CodeBlock } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { Button, buttonVariants } from '$comp/ui/button';
    import { Spinner } from '$comp/ui/spinner';
    import * as Tabs from '$comp/ui/tabs';
    import {
        getOrgSavedViewsExportMutation,
        getPredefinedSavedViewsMutation,
        postForceUpdatePredefinedSavedViewsMutation,
        putPredefinedSavedViewsMutation
    } from '$features/admin/api.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import JsonCodeEditor from '$features/shared/components/json-code-editor.svelte';
    import { ProblemDetails } from '@foundatiofx/fetchclient';
    import Pencil from '@lucide/svelte/icons/pencil';
    import Save from '@lucide/svelte/icons/save';
    import { onMount } from 'svelte';
    import { toast } from 'svelte-sonner';

    let predefinedJson = $state('');
    let predefinedTab = $state('config');
    let savedPredefinedJson = $state('');
    let isEditing = $state(false);
    let forceUpdateOpen = $state(false);
    let toastId = $state<number | string>();

    const predefinedSavedViews = getPredefinedSavedViewsMutation();
    const organizationExport = getOrgSavedViewsExportMutation();
    const savePredefined = putPredefinedSavedViewsMutation();
    const forceUpdatePredefined = postForceUpdatePredefinedSavedViewsMutation();
    const organizationId = $derived(organization.current ?? '');
    const hasUnsavedPredefinedChanges = $derived(predefinedTab === 'config' && predefinedJson !== savedPredefinedJson);

    function getErrorMessage(error: unknown, fallback: string) {
        return error instanceof ProblemDetails ? error.title : fallback;
    }

    async function loadPredefinedSavedViews() {
        toast.dismiss(toastId);

        try {
            predefinedJson = await predefinedSavedViews.mutateAsync();
            savedPredefinedJson = predefinedJson;
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
            savedPredefinedJson = predefinedJson;
            isEditing = false;
            toastId = toast.success('Predefined saved views updated successfully.');
        } catch (error: unknown) {
            toastId = toast.error(`An error occurred while saving predefined saved views: ${getErrorMessage(error, 'Please try again.')}`);
        }
    }

    function handleTabChange(value: string) {
        predefinedTab = value;
        isEditing = false;
        if (value === 'config') {
            void loadPredefinedSavedViews();
        } else {
            void loadOrganizationViews();
        }
    }

    function handleCancelEdit() {
        predefinedJson = savedPredefinedJson;
        isEditing = false;
    }

    async function handleForceUpdatePredefined() {
        toast.dismiss(toastId);

        try {
            await forceUpdatePredefined.mutateAsync();
            forceUpdateOpen = false;
            toastId = toast.success('Force update of matching organization saved views was queued.');
        } catch (error: unknown) {
            toastId = toast.error(`An error occurred while queuing the force update: ${getErrorMessage(error, 'Please try again.')}`);
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

        <div class="flex gap-2">
            {#if predefinedTab === 'config'}
                {#if isEditing}
                    <Button variant="outline" onclick={handleCancelEdit}>Cancel</Button>
                {:else}
                    <Button variant="outline" onclick={() => (isEditing = true)}>
                        <Pencil class="size-4" aria-hidden="true" />
                        Edit JSON
                    </Button>
                {/if}
            {/if}
            <CopyToClipboardButton value={predefinedJson} variant="outline" />
        </div>
    </div>

    {#if isEditing && predefinedTab === 'config'}
        <JsonCodeEditor bind:value={predefinedJson} aria-label="Predefined saved views JSON" />
    {:else}
        <CodeBlock code={predefinedJson} language="json" class="max-h-[60vh] min-h-96 overflow-auto text-xs" />
    {/if}

    <div class="flex flex-col justify-end gap-2 sm:flex-row">
        <Button
            variant="destructive"
            disabled={forceUpdatePredefined.isPending || savePredefined.isPending || hasUnsavedPredefinedChanges}
            onclick={() => {
                forceUpdateOpen = true;
            }}
        >
            Force Update
        </Button>
        {#if isEditing && predefinedTab === 'config'}
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
        {/if}
    </div>
</div>

<AlertDialog.Root bind:open={forceUpdateOpen}>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Force Update Matching Organization Saved Views</AlertDialog.Title>
            <AlertDialog.Description>
                This queues a background job that overwrites every organization-wide saved view whose predefined key matches a saved system definition. It also
                creates missing predefined views for organizations with matching views. It discards customizations to matching views. Private and unmatched
                views are not changed. Unsaved JSON edits are not included.
            </AlertDialog.Description>
        </AlertDialog.Header>

        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action
                class={buttonVariants({
                    variant: 'destructive'
                })}
                disabled={forceUpdatePredefined.isPending}
                onclick={handleForceUpdatePredefined}
            >
                {forceUpdatePredefined.isPending ? 'Queuing...' : 'Force Update'}
            </AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
