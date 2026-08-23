<script lang="ts">
    import type { ErrorInfo, SourceMapFailureInfo } from '$features/events/models/event-data';

    import { resolve } from '$app/paths';
    import { Button } from '$comp/ui/button';
    import * as Popover from '$comp/ui/popover';
    import TriangleAlert from '@lucide/svelte/icons/triangle-alert';

    interface Props {
        error: ErrorInfo;
        projectId: string;
    }

    let { error, projectId }: Props = $props();

    const sourceMapStatus = $derived(error.data?.['@source_map']);
    const failures = $derived(sourceMapStatus?.failures ?? []);
    const processingTruncated = $derived(sourceMapStatus?.processing_truncated === true);
    const title = $derived(sourceMapStatus?.status === 'partial' ? 'Stack trace partially symbolicated' : 'Source map unavailable');

    function getFailureDescription(failure: SourceMapFailureInfo): string {
        switch (failure.reason) {
            case 'invalid':
                return 'The downloaded source map is invalid or unsupported.';
            case 'no_matching_mapping':
                return 'The source map does not contain this generated location.';
            case 'not_found':
                return 'No usable source map could be downloaded.';
            case 'timeout':
                return 'The source map download timed out.';
            default:
                return 'The source map could not be downloaded.';
        }
    }
</script>

{#if failures.length > 0 || processingTruncated}
    <Popover.Root>
        <Popover.Trigger>
            {#snippet child({ props })}
                <Button {...props} size="sm" title="View source map details" variant="outline">
                    <TriangleAlert class="size-3.5 text-amber-500" />
                    {title}
                </Button>
            {/snippet}
        </Popover.Trigger>
        <Popover.Content align="end" class="w-96 max-w-[calc(100vw-2rem)]">
            <Popover.Header>
                <Popover.Title>{title}</Popover.Title>
                <Popover.Description class="text-xs">
                    {#if failures.length > 0}
                        <p>
                            Exceptionless couldn't map {failures.length === 1 ? 'a JavaScript file' : `${failures.length} JavaScript files`} to original source. The
                            stack trace may be minified. Uploading a source map will improve new events.
                        </p>
                        <ul class="mt-1.5 space-y-1">
                            {#each failures as failure (failure.generated_file_name)}
                                <li>
                                    <span class="font-mono break-all">{failure.generated_file_name}</span>
                                    <span> — {getFailureDescription(failure)}</span>
                                </li>
                            {/each}
                            {#if sourceMapStatus?.truncated}<li>Additional generated files were omitted.</li>{/if}
                        </ul>
                    {/if}
                    {#if processingTruncated}
                        <p class:mt-1.5={failures.length > 0}>
                            Exceptionless stopped checking source maps after reaching the stack-frame processing limit. Some frames may remain minified.
                        </p>
                    {/if}
                </Popover.Description>
            </Popover.Header>
            <div>
                <Button
                    href={resolve('/(app)/project/[projectId]/source-maps', {
                        projectId
                    })}
                    size="sm"
                    variant="outline">Manage source maps</Button
                >
            </div>
        </Popover.Content>
    </Popover.Root>
{/if}
