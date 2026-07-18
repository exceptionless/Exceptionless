<script lang="ts">
    import type { SourceMapArtifact } from '$features/projects/models';

    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import ErrorMessage from '$comp/error-message.svelte';
    import Bytes from '$comp/formatters/bytes.svelte';
    import DateTime from '$comp/formatters/date-time.svelte';
    import { A, H4, Muted } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { Badge } from '$comp/ui/badge';
    import { Button, buttonVariants } from '$comp/ui/button';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Spinner } from '$comp/ui/spinner';
    import * as Table from '$comp/ui/table';
    import { deleteSourceMapMutation, getSourceMapsQuery, postSourceMapMutation } from '$features/projects/api.svelte';
    import { type SourceMapUploadFormData, SourceMapUploadSchema } from '$features/projects/schemas';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import ArrowLeft from '@lucide/svelte/icons/arrow-left';
    import Trash from '@lucide/svelte/icons/trash-2';
    import Upload from '@lucide/svelte/icons/upload';
    import { createForm } from '@tanstack/svelte-form';
    import { toast } from 'svelte-sonner';

    const projectId = $derived(page.params.projectId || '');
    const request = {
        route: {
            get id() {
                return projectId;
            }
        }
    };
    const sourceMapsQuery = getSourceMapsQuery(request);
    const uploadSourceMap = postSourceMapMutation(request);
    const deleteSourceMap = deleteSourceMapMutation(request);

    let fileInput = $state<HTMLInputElement | null>(null);
    let sourceMapToDelete = $state<SourceMapArtifact>();
    let showDeleteDialog = $state(false);

    const form = createForm(() => ({
        defaultValues: {
            file: null,
            generated_file_url: ''
        } as SourceMapUploadFormData,
        validators: {
            onSubmit: SourceMapUploadSchema,
            onSubmitAsync: async ({ value }) => {
                try {
                    await uploadSourceMap.mutateAsync({ file: value.file!, generated_file_url: value.generated_file_url });
                    toast.success('Source map uploaded. New events will be symbolicated before stacking.');
                    form.reset();
                    if (fileInput) {
                        fileInput.value = '';
                    }

                    return null;
                } catch (error: unknown) {
                    toast.error('Unable to upload the source map.');
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }

                    return { form: 'An unexpected error occurred.' };
                }
            }
        }
    }));

    async function removeSourceMap() {
        if (!sourceMapToDelete) {
            return;
        }

        try {
            const deleted = await deleteSourceMap.mutateAsync(sourceMapToDelete.id);
            if (!deleted) {
                toast.error('The source map no longer exists.');
                return;
            }

            toast.success('Source map deleted.');
            showDeleteDialog = false;
            sourceMapToDelete = undefined;
        } catch {
            toast.error('Unable to delete the source map.');
        }
    }

    function confirmDelete(sourceMap: SourceMapArtifact) {
        sourceMapToDelete = sourceMap;
        showDeleteDialog = true;
    }
</script>

<div class="space-y-6">
    <section class="space-y-2">
        <H4>Source Map Discovery</H4>
        <Muted>
            Exceptionless automatically downloads publicly available source maps over HTTPS using the generated file's SourceMap header or sourceMappingURL.
            Upload a map only when it is private or not published with the generated JavaScript.
        </Muted>
    </section>

    <section class="space-y-4">
        <H4>Upload Source Map</H4>
        <Muted>
            Automating uploads from CI/CD? Create a project-scoped
            <A href={resolve('/(app)/project/[projectId]/api-keys', { projectId })}>source map upload token</A> and use it with the source map API.
        </Muted>
        <form
            class="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(16rem,0.5fr)_auto] lg:items-end"
            onsubmit={(event) => {
                event.preventDefault();
                event.stopPropagation();
                form.handleSubmit();
            }}
        >
            <form.Subscribe selector={(state) => state.errors}>
                {#snippet children(errors)}
                    <div class="lg:col-span-3"><ErrorMessage message={getFormErrorMessages(errors)} /></div>
                {/snippet}
            </form.Subscribe>

            <form.Field name="generated_file_url">
                {#snippet children(field)}
                    <Field.Field data-invalid={ariaInvalid(field)}>
                        <Field.Label for={field.name}>Generated JavaScript URL</Field.Label>
                        <Input
                            id={field.name}
                            name={field.name}
                            placeholder="https://cdn.example.com/assets/app.min.js"
                            required
                            type="url"
                            value={field.state.value}
                            onblur={field.handleBlur}
                            oninput={(event) => field.handleChange(event.currentTarget.value)}
                            aria-invalid={ariaInvalid(field)}
                        />
                        <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                    </Field.Field>
                {/snippet}
            </form.Field>

            <form.Field name="file">
                {#snippet children(field)}
                    <Field.Field data-invalid={ariaInvalid(field)}>
                        <Field.Label for={field.name}>Source map</Field.Label>
                        <Input
                            accept=".map,application/json"
                            bind:ref={fileInput}
                            id={field.name}
                            name={field.name}
                            required
                            type="file"
                            onchange={(event) => field.handleChange(event.currentTarget.files?.[0] ?? null)}
                            aria-invalid={ariaInvalid(field)}
                        />
                        <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                    </Field.Field>
                {/snippet}
            </form.Field>

            <form.Subscribe selector={(state) => state.isSubmitting}>
                {#snippet children(isSubmitting)}
                    <Button type="submit" disabled={isSubmitting}>
                        {#if isSubmitting}<Spinner />{:else}<Upload class="size-4" />{/if}
                        {isSubmitting ? 'Uploading...' : 'Upload'}
                    </Button>
                {/snippet}
            </form.Subscribe>
        </form>
    </section>

    <section class="space-y-3">
        <H4>Available Source Maps</H4>
        <div class="rounded-md border">
            <Table.Root>
                <Table.Header>
                    <Table.Row>
                        <Table.Head>Generated file</Table.Head>
                        <Table.Head>Source</Table.Head>
                        <Table.Head>Size</Table.Head>
                        <Table.Head>Added</Table.Head>
                        <Table.Head class="w-12"><span class="sr-only">Actions</span></Table.Head>
                    </Table.Row>
                </Table.Header>
                <Table.Body>
                    {#if sourceMapsQuery.isLoading}
                        <Table.Row><Table.Cell colspan={5} class="h-24 text-center"><Spinner /></Table.Cell></Table.Row>
                    {:else if sourceMapsQuery.isError}
                        <Table.Row
                            ><Table.Cell colspan={5} class="h-24 text-center"><ErrorMessage message="Unable to load source maps." /></Table.Cell></Table.Row
                        >
                    {:else if !sourceMapsQuery.data?.length}
                        <Table.Row
                            ><Table.Cell colspan={5} class="text-muted-foreground h-24 text-center"
                                >No source maps have been discovered or uploaded yet.</Table.Cell
                            ></Table.Row
                        >
                    {:else}
                        {#each sourceMapsQuery.data as sourceMap (sourceMap.id)}
                            <Table.Row>
                                <Table.Cell class="max-w-xl break-all font-mono text-xs">{sourceMap.generated_file_url}</Table.Cell>
                                <Table.Cell>
                                    <Badge variant={sourceMap.is_auto_downloaded ? 'secondary' : 'outline'}>
                                        {sourceMap.is_auto_downloaded ? 'Automatic' : 'Uploaded'}
                                    </Badge>
                                    {#if sourceMap.file_name}<div class="text-muted-foreground mt-1 text-xs">{sourceMap.file_name}</div>{/if}
                                </Table.Cell>
                                <Table.Cell><Bytes value={sourceMap.size} /></Table.Cell>
                                <Table.Cell><DateTime value={sourceMap.created_utc} /></Table.Cell>
                                <Table.Cell>
                                    <Button
                                        aria-label={`Delete source map for ${sourceMap.generated_file_url}`}
                                        disabled={deleteSourceMap.isPending}
                                        onclick={() => confirmDelete(sourceMap)}
                                        size="icon"
                                        variant="ghost"
                                    >
                                        <Trash class="size-4" />
                                    </Button>
                                </Table.Cell>
                            </Table.Row>
                        {/each}
                    {/if}
                </Table.Body>
            </Table.Root>
        </div>
    </section>

    <div class="border-border flex border-t pt-4 sm:justify-end">
        <Button variant="secondary" href={`${resolve('/(app)/project/[projectId]/settings', { projectId })}#error-stacking`}>
            <ArrowLeft class="mr-2 size-4" aria-hidden="true" /> Back to Error Stacking
        </Button>
    </div>
</div>

<AlertDialog.Root bind:open={showDeleteDialog}>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Delete Source Map</AlertDialog.Title>
            <AlertDialog.Description>
                Delete the source map for <span class="break-all font-mono text-xs">{sourceMapToDelete?.generated_file_url}</span>? Future events will use
                automatic discovery or retain their generated stack frames.
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action class={buttonVariants({ variant: 'destructive' })} onclick={removeSourceMap}>Delete Source Map</AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
