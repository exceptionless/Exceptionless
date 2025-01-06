<script lang="ts">
    import Number from '$comp/formatters/Number.svelte';
    import { A, P } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import * as Form from '$comp/ui/form';
    import { Input } from '$comp/ui/input';
    import { defaults, superForm } from 'sveltekit-superforms';
    import { classvalidatorClient } from 'sveltekit-superforms/adapters';
    import { debounce } from 'throttle-debounce';
    import IconDocumentation from '~icons/mdi/help-circle';

    import { FixedInVersionForm } from '../../models';

    interface Props {
        count?: number;
        open: boolean;
        save: (version?: string) => Promise<void>;
    }

    let { count = 1, open = $bindable(), save }: Props = $props();

    const form = superForm(defaults(new FixedInVersionForm(), classvalidatorClient(FixedInVersionForm)), {
        dataType: 'json',
        onChange() {
            debouncedUpdateVersionToSemanticVersion();
        },
        onSubmit() {
            updateVersionToSemanticVersion();
        },
        async onUpdate({ form }) {
            if (!form.valid) {
                return;
            }

            await save(form.data.version);
            open = false;
        },
        SPA: true,
        validators: classvalidatorClient(FixedInVersionForm)
    });

    const { enhance, form: formData } = form;

    const debouncedUpdateVersionToSemanticVersion = debounce(1000, updateVersionToSemanticVersion);
    function updateVersionToSemanticVersion() {
        const version = $formData.version;
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
            $formData.version = transformedInput;
        }
    }
</script>

<AlertDialog.Root bind:open onOpenChange={() => form.reset()}>
    <AlertDialog.Content class="sm:max-w-[425px]">
        <form method="POST" use:enhance>
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

            <P class="pb-4">
                <strong>Optional:</strong> Please enter the version in which
                {#if count === 1}
                    the stack has
                {:else}
                    these stacks have
                {/if}
                been fixed. Any submitted occurrences with a lower version will not cause a regression.
                <A class="inline-flex" href="https://exceptionless.com/docs/versioning/" target="_blank" title="Versioning Documentation"
                    ><IconDocumentation /></A
                >
            </P>

            <Form.Field {form} name="version">
                <Form.Control>
                    {#snippet children({ props })}
                        <Form.Label>Version</Form.Label>
                        <Input {...props} bind:value={$formData.version} type="text" placeholder="Optional Semantic Version (Example: 1.2.3)" />
                    {/snippet}
                </Form.Control>
                <Form.Description />
                <Form.FieldErrors />
            </Form.Field>

            <AlertDialog.Footer>
                <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                <AlertDialog.Action>
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
