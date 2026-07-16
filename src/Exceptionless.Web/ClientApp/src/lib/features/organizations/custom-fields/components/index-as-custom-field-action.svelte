<script lang="ts">
    import ErrorMessage from '$comp/error-message.svelte';
    import { Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import * as Field from '$comp/ui/field';
    import * as Select from '$comp/ui/select';
    import { Spinner } from '$comp/ui/spinner';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing/upgrade-required.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import {
        createCustomFieldMutation,
        getCustomFieldsQuery,
        INDEX_TYPE_DESCRIPTIONS,
        INDEX_TYPE_LABELS,
        INDEX_TYPES,
        type IndexType
    } from '$features/organizations/custom-fields';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import Database from '@lucide/svelte/icons/database';
    import { toast } from 'svelte-sonner';

    interface Props {
        fieldName: string;
    }

    let { fieldName }: Props = $props();

    const organizationId = $derived(organization.current ?? '');

    const customFieldsQuery = getCustomFieldsQuery({
        route: {
            get organizationId() {
                return organizationId;
            }
        }
    });

    const isReservedSystemField = $derived(['haserror', 'sessionend'].includes(fieldName.toLowerCase()));
    const isAlreadyIndexed = $derived(customFieldsQuery.data?.some((field) => field.name.toLowerCase() === fieldName.toLowerCase()) ?? false);
    const canCreate = $derived(!isReservedSystemField && customFieldsQuery.isSuccess && !isAlreadyIndexed);

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
</script>

{#if organizationId && canCreate}
    <DropdownMenu.Item onclick={() => (showDialog = true)} title="Index this field for filtering">
        <Database data-icon="inline-start" />
        Index as Custom Field
    </DropdownMenu.Item>

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
                class="flex flex-col gap-4"
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
                            <Select.Group>
                                {#each INDEX_TYPES as type (type)}
                                    <Select.Item value={type} class="flex flex-col items-start gap-0.5 py-2">
                                        <span class="font-medium">{INDEX_TYPE_LABELS[type]}</span>
                                        <span class="text-muted-foreground text-xs">{INDEX_TYPE_DESCRIPTIONS[type]}</span>
                                    </Select.Item>
                                {/each}
                            </Select.Group>
                        </Select.Content>
                    </Select.Root>
                    <Muted>Choose the type that best matches this field's data.</Muted>
                </Field.Field>

                <Dialog.Footer>
                    <Button variant="outline" type="button" onclick={() => (showDialog = false)}>Cancel</Button>
                    <Button type="submit" disabled={createField.isPending}>
                        {#if createField.isPending}
                            <Spinner data-icon="inline-start" />
                        {/if}
                        Index Field
                    </Button>
                </Dialog.Footer>
            </form>
        </Dialog.Content>
    </Dialog.Root>
{/if}
