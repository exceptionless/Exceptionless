<script lang="ts">
    import ErrorMessage from '$comp/error-message.svelte';
    import Number from '$comp/formatters/number.svelte';
    import { A, P } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import Documentation from '@lucide/svelte/icons/help-circle';
    import { createForm } from '@tanstack/svelte-form';
    import { debounce } from 'throttle-debounce';

    import { type FixedInVersionFormData, FixedInVersionSchema } from '../../schemas';

    interface Props {
        count?: number;
        open: boolean;
        save: (version?: string) => Promise<void>;
    }

    let { count = 1, open = $bindable(), save }: Props = $props();

    const form = createForm(() => ({
        defaultValues: {
            version: ''
        } as FixedInVersionFormData,
        validators: {
            onSubmit: FixedInVersionSchema,
            onSubmitAsync: async ({ value }) => {
                try {
                    updateVersionToSemanticVersion();
                    await save(value.version || undefined);
                    open = false;
                    return null;
                } catch (error: unknown) {
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }
                    return { form: 'An unexpected error occurred.' };
                }
            }
        }
    }));

    $effect(() => {
        if (open) {
            form.reset();
        }
    });

    const debouncedUpdateVersionToSemanticVersion = debounce(1000, updateVersionToSemanticVersion);
    function updateVersionToSemanticVersion() {
        const version = form.state.values.version;
        const isVersionRegex = /^(\d+)\.(\d+)\.?(\d+)?\.?(\d+)?$/;
        if (!version || !isVersionRegex.test(version)) {
            return;
        }

        let transformedInput = '';
        const isTwoPartVersion = /^(\d+)\.(\d+)$/;
        const isFourPartVersion = /^(\d+)\.(\d+)\.(\d+)\.(\d+)$/;
        if (isTwoPartVersion.test(version)) {
            transformedInput = version.replace(isTwoPartVersion, '$1.$2.0');
        } else if (isFourPartVersion.test(version)) {
            transformedInput = version.replace(isFourPartVersion, '$1.$2.$3-$4');
        }

        if (transformedInput !== '') {
            form.setFieldValue('version', transformedInput);
        }
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content class="sm:max-w-[425px]">
        <form
            onsubmit={(e) => {
                e.preventDefault();
                e.stopPropagation();
                form.handleSubmit();
            }}
        >
            <AlertDialog.Header>
                <AlertDialog.Title>
                    {#if count === 1}
                        Mark Stack As Fixed
                    {:else}
                        Mark <Number value={count} /> Stacks As Fixed
                    {/if}
                </AlertDialog.Title>
                <AlertDialog.Description>
                    Marks
                    {#if count === 1}
                        the stack
                    {:else}
                        <Number value={count} /> stacks
                    {/if}
                    as fixed. This will also prevent error occurrences from being displayed in the dashboard.
                </AlertDialog.Description>
            </AlertDialog.Header>

            <form.Subscribe selector={(state) => state.errors}>
                {#snippet children(errors)}
                    <ErrorMessage message={getFormErrorMessages(errors)}></ErrorMessage>
                {/snippet}
            </form.Subscribe>

            <P class="pb-4">
                <strong>Optional:</strong> Please enter the version in which
                {#if count === 1}
                    the stack has
                {:else}
                    these stacks have
                {/if}
                been fixed. Any submitted occurrences with a lower version will not cause a regression.
                <A class="inline-flex" href="https://exceptionless.com/docs/versioning/" target="_blank" title="Versioning Documentation"><Documentation /></A>
            </P>

            <form.Field name="version">
                {#snippet children(field)}
                    <Field.Field data-invalid={field.state.meta.errors.length > 0 ? true : undefined}>
                        <Field.Label for={field.name}>Version</Field.Label>
                        <Input
                            id={field.name}
                            name={field.name}
                            type="text"
                            placeholder="Optional Semantic Version (Example: 1.2.3)"
                            value={field.state.value}
                            onblur={field.handleBlur}
                            oninput={(e) => {
                                field.handleChange(e.currentTarget.value);
                                debouncedUpdateVersionToSemanticVersion();
                            }}
                            aria-invalid={field.state.meta.errors.length > 0 ? true : undefined}
                        />
                        <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                    </Field.Field>
                {/snippet}
            </form.Field>

            <AlertDialog.Footer>
                <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                <AlertDialog.Action type="submit">
                    Mark
                    {#if count === 1}
                        Stack
                    {:else}
                        <Number value={count} /> Stacks
                    {/if}
                    Fixed
                </AlertDialog.Action>
            </AlertDialog.Footer>
        </form>
    </AlertDialog.Content>
</AlertDialog.Root>
