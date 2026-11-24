<script lang="ts">
    import FlaskConical from '@lucide/svelte/icons/flask-conical';
    import { toast } from 'svelte-sonner';

    import type { ButtonSize, ButtonVariant } from '#comp/ui/button/index.ts';

    import * as AlertDialog from '#comp/ui/alert-dialog/index.ts';
    import { Button } from '#comp/ui/button/index.ts';
    import { Spinner } from '#comp/ui/spinner/index.ts';
    import { generateSampleData } from '#features/projects/api.svelte.ts';

    interface Props {
        onQueued?: () => void;
        projectId: string;
        projectName?: string;
        size?: ButtonSize;
        variant?: ButtonVariant;
    }

    let { onQueued, projectId, projectName, size = 'default', variant = 'outline' }: Props = $props();

    let open = $state(false);
    const sampleDataMutation = generateSampleData({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    async function addSampleData(): Promise<void> {
        try {
            await sampleDataMutation.mutateAsync();
            open = false;
            toast.success('Sample events are on their way', {
                description: 'They will show up in a moment.'
            });
            onQueued?.();
        } catch {
            toast.error('We could not add sample data. Please try again.');
        }
    }
</script>

<Button disabled={sampleDataMutation.isPending} onclick={() => (open = true)} {size} {variant}>
    {#if sampleDataMutation.isPending}
        <Spinner data-icon="inline-start" />
    {:else}
        <FlaskConical data-icon="inline-start" aria-hidden="true" />
    {/if}
    Explore with sample data
</Button>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Add sample data{projectName ? ` to ${projectName}` : ''}?</AlertDialog.Title>
            <AlertDialog.Description>
                This adds a batch of demo events to this project so you can explore Exceptionless before your app is connected. You can clear them anytime with
                Reset Project Data in the project settings.
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Not now</AlertDialog.Cancel>
            <Button disabled={sampleDataMutation.isPending} onclick={addSampleData}>
                {#if sampleDataMutation.isPending}<Spinner data-icon="inline-start" />{/if}
                Add sample data
            </Button>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
