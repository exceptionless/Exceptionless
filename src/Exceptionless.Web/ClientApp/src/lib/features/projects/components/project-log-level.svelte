<script lang="ts">
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { Skeleton } from '$comp/ui/skeleton';
    import { getLogLevel, getLogLevelDisplayName, type LogLevel } from '$features/events/models/event-data';
    import { logLevels } from '$features/events/options';
    import { deleteProjectConfig, getProjectConfig, postProjectConfig } from '$features/projects/api.svelte';
    import { Button } from '$features/shared/components/ui/button';
    import ChevronDown from '@lucide/svelte/icons/chevron-down';
    import { toast } from 'svelte-sonner';

    interface Props {
        projectId: string;
        source: string;
    }

    let { projectId, source }: Props = $props();
    const projectConfigQuery = getProjectConfig({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    const updateProjectConfig = postProjectConfig({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    const removeProjectConfig = deleteProjectConfig({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    async function setLogLevel(level: LogLevel) {
        await updateProjectConfig.mutateAsync({
            key: `@@log:${source}`,
            value: level
        });

        toast.success(`Successfully updated Log level to ${getLogLevelDisplayName(level)}`);
    }

    async function revertToDefaultLogLevel() {
        removeProjectConfig.mutateAsync({
            key: `@@log:${source}`
        });
        toast.success(`Successfully reverted to default (${defaultLevelDisplayName}) log level`);
    }

    const configSettings = $derived(projectConfigQuery.data?.settings ?? {});
    const level = $derived(getLogLevel(configSettings[`@@log:${source ?? ''}`]));
    const defaultLevel = $derived(getDefaultLogLevel(configSettings, source ?? ''));
    const defaultLevelDisplayName = $derived(getLogLevelDisplayName(defaultLevel));

    function getDefaultLogLevel(configSettings: Record<string, string>, source: string): LogLevel | null {
        const sourcePrefix = '@@log:';

        // sort object keys longest first, then alphabetically.
        const sortedKeys = Object.keys(configSettings).sort(function (a, b) {
            return b.length - a.length || a.localeCompare(b);
        });

        for (const index in sortedKeys) {
            const key = sortedKeys[index];
            if (!key) {
                continue;
            }

            if (!key.toLowerCase().startsWith(sourcePrefix)) {
                continue;
            }

            const cleanKey = key.substring(sourcePrefix.length);
            if (cleanKey.toLowerCase() === source.toLowerCase()) {
                continue;
            }

            // check for wildcard match
            if (isMatch(source, [cleanKey])) {
                return getLogLevel(configSettings[key]);
            }
        }

        return null;
    }

    // TODO: Move to string utils
    function isMatch(input: string, patterns: string[], ignoreCase = true): boolean {
        const trimmedInput = ignoreCase ? input.toLowerCase().trim() : input.trim();

        return (patterns || []).some((pattern) => {
            let trimmedPattern = ignoreCase ? pattern.toLowerCase().trim() : pattern.trim();
            if (trimmedPattern.length <= 0) {
                return false;
            }

            const startsWithWildcard = trimmedPattern[0] === '*';
            if (startsWithWildcard) {
                trimmedPattern = trimmedPattern.slice(1);
            }

            const endsWithWildcard = trimmedPattern[trimmedPattern.length - 1] === '*';
            if (endsWithWildcard) {
                trimmedPattern = trimmedPattern.substring(0, trimmedPattern.length - 1);
            }

            if (startsWithWildcard && endsWithWildcard) {
                return trimmedPattern.length <= trimmedInput.length && trimmedInput.indexOf(trimmedPattern) !== -1;
            }

            if (startsWithWildcard) {
                return trimmedInput.endsWith(trimmedPattern);
            }

            if (endsWithWildcard) {
                return trimmedInput.startsWith(trimmedPattern);
            }

            return trimmedInput === trimmedPattern;
        });
    }
</script>

{#if projectConfigQuery.isSuccess}
    <DropdownMenu.Root>
        <DropdownMenu.Trigger>
            {#snippet child({ props })}
                <Button {...props} variant="outline">
                    {#if level}
                        Log Level: {getLogLevelDisplayName(level)}
                    {:else if defaultLevel}
                        Log Level: {defaultLevelDisplayName} (Default)
                    {:else}
                        Select a Default Log Level
                    {/if}
                    <ChevronDown class="size-4" />
                </Button>
            {/snippet}
        </DropdownMenu.Trigger>
        <DropdownMenu.Content>
            <DropdownMenu.Group>
                <DropdownMenu.GroupHeading>Log Level</DropdownMenu.GroupHeading>
                <DropdownMenu.Separator />

                {#each logLevels as lvl (lvl.value)}
                    <DropdownMenu.CheckboxItem
                        checked={lvl.value === level}
                        title={`Update Log Level to ${lvl.label}`}
                        onclick={() => setLogLevel(lvl.value)}
                        disabled={updateProjectConfig.isPending}>{lvl.label}</DropdownMenu.CheckboxItem
                    >
                {/each}
                {#if level && source !== '*'}
                    <DropdownMenu.Separator />
                    <DropdownMenu.Item
                        title={`Reset to default (${defaultLevelDisplayName})`}
                        onclick={revertToDefaultLogLevel}
                        disabled={removeProjectConfig.isPending}>Default ({defaultLevelDisplayName})</DropdownMenu.Item
                    >
                {/if}
            </DropdownMenu.Group>
        </DropdownMenu.Content>
    </DropdownMenu.Root>
{:else}
    <Skeleton class="h-[36px] w-[135px]" />
{/if}
