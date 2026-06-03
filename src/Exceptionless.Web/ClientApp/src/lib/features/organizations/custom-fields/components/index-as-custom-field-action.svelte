<script lang="ts">
    import ErrorMessage from '$comp/error-message.svelte';
    import { Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import * as Field from '$comp/ui/field';
    import * as Select from '$comp/ui/select';
    import { Spinner } from '$comp/ui/spinner';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing/upgrade-required.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { createCustomFieldMutation, INDEX_TYPE_DESCRIPTIONS, INDEX_TYPE_LABELS, INDEX_TYPES, type IndexType } from '$features/organizations/custom-fields';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import Database from '@lucide/svelte/icons/database';
    import { toast } from 'svelte-sonner';

    interface Props {
        fieldName: string;
    }

    let { fieldName }: Props = $props();

    const organizationId = $derived(organization.current ?? '');

    let showDialog = $state(false);
    let selectedType = $state<IndexType>('keyword');
    let error = $state('');

    const createField = createCustomFieldMutation({
        route: {
            get organizationId() {
                return organizationId;
            }
        }
    });

    async function handleCreate() {
        error = '';
        try {
            await createField.mutateAsync({
                indexType: selectedType,
                name: fieldName
            });
            toast.success(`"${fieldName}" is now indexed as a custom field. Future events will include it in search.`);
            showDialog = false;
        } catch (e: unknown) {
            if (showBillingDialogOnUpgradeProblem(e, organizationId, () => handleCreate())) {
                error = '';
                return;
            }

            if (e instanceof ProblemDetails) {
                error = e.errors?.['general']?.[0] ?? e.title ?? e.detail ?? 'An error occurred.';
            } else {
                error = 'An unexpected error occurred.';
            }
        }
    }

    $effect(() => {
        if (!showDialog) {
            document.body.style.removeProperty('pointer-events');
            document.body.style.removeProperty('overflow');
        }
    });
</script>

{#if organizationId}
    <button
        class="data-[highlighted]:bg-accent data-[highlighted]:text-accent-foreground relative flex w-full cursor-default items-center gap-2 rounded-sm px-2 py-1.5 text-sm outline-none select-none data-[disabled]:pointer-events-none data-[disabled]:opacity-50"
        onclick={() => (showDialog = true)}
        title="Index this field for filtering"
    >
        <Database class="mr-2 size-4" />
        Index as Custom Field
    </button>

    <Dialog.Root bind:open={showDialog}>
        <Dialog.Content class="sm:max-w-[400px]">
            <Dialog.Header>
                <Dialog.Title>Index "{fieldName}" as Custom Field</Dialog.Title>
                <Dialog.Description>
                    This will start indexing "{fieldName}" from future events, making it available for filtering and search. Existing events will not be
                    retroactively indexed.
                </Dialog.Description>
            </Dialog.Header>

            <form
                class="space-y-4"
                onsubmit={(e) => {
                    e.preventDefault();
                    handleCreate();
                }}
            >
                {#if error}
                    <ErrorMessage message={error} />
                {/if}

                <Field.Field>
                    <Field.Label for="index-type">Index Type</Field.Label>
                    <Select.Root type="single" value={selectedType} onValueChange={(v) => (selectedType = (v ?? 'keyword') as IndexType)}>
                        <Select.Trigger id="index-type" class="w-full">
                            <span class="font-medium">{INDEX_TYPE_LABELS[selectedType]}</span>
                            <span class="text-muted-foreground ml-2 text-xs">{INDEX_TYPE_DESCRIPTIONS[selectedType]}</span>
                        </Select.Trigger>
                        <Select.Content class="max-h-72">
                            {#each INDEX_TYPES as type (type)}
                                <Select.Item value={type} class="flex flex-col items-start gap-0.5 py-2">
                                    <span class="font-medium">{INDEX_TYPE_LABELS[type]}</span>
                                    <span class="text-muted-foreground text-xs">{INDEX_TYPE_DESCRIPTIONS[type]}</span>
                                </Select.Item>
                            {/each}
                        </Select.Content>
                    </Select.Root>
                    <Muted>Choose the type that best matches this field's data.</Muted>
                </Field.Field>

                <Dialog.Footer>
                    <Button variant="outline" type="button" onclick={() => (showDialog = false)}>Cancel</Button>
                    <Button type="submit" disabled={createField.isPending}>
                        {#if createField.isPending}
                            <Spinner class="mr-2" />
                        {/if}
                        Index Field
                    </Button>
                </Dialog.Footer>
            </form>
        </Dialog.Content>
    </Dialog.Root>
{/if}
