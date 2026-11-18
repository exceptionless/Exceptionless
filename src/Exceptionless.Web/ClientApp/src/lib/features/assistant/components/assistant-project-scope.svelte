<script lang="ts">
    import ChevronDown from '@lucide/svelte/icons/chevron-down';
    import Folder from '@lucide/svelte/icons/folder';
    import Layers2 from '@lucide/svelte/icons/layers-2';

    import * as Command from '#comp/ui/command/index.ts';
    import * as InputGroup from '#comp/ui/input-group/index.ts';
    import * as Popover from '#comp/ui/popover/index.ts';

    import type { AssistantProject } from '../models';

    interface Props {
        disabled?: boolean;
        onChange: (projectId: string | undefined) => void;
        projects: AssistantProject[];
        /** The project Exie searches; undefined means all projects. */
        value?: string;
    }

    let { disabled = false, onChange, projects, value }: Props = $props();

    /** Searching only helps once the list no longer fits at a glance. */
    const SEARCH_THRESHOLD = 7;
    const ALL_PROJECTS = '__all__';

    let open = $state(false);
    let sortedProjects = $derived(projects.toSorted((left, right) => left.name.localeCompare(right.name)));
    let label = $derived(value ? (projects.find((project) => project.id === value)?.name ?? 'This project') : 'All projects');

    function select(projectId: string | undefined): void {
        open = false;
        if (projectId !== value) {
            onChange(projectId);
        }
    }
</script>

<Popover.Root bind:open>
    <Popover.Trigger>
        {#snippet child({ props })}
            <InputGroup.Button
                {...props}
                aria-label={`Project scope: ${label}`}
                class="text-muted-foreground hover:text-foreground h-6 max-w-48 min-w-0 text-xs font-medium"
                data-testid="assistant-project-scope"
                {disabled}
                size="xs"
                title={`Exie searches ${value ? label : 'all projects'}`}
                variant="ghost"
            >
                {#if value}
                    <Folder aria-hidden="true" />
                {:else}
                    <Layers2 aria-hidden="true" />
                {/if}
                <span class="min-w-0 truncate">{label}</span>
                <ChevronDown aria-hidden="true" class="opacity-60" />
            </InputGroup.Button>
        {/snippet}
    </Popover.Trigger>
    <Popover.Content align="start" class="w-64 p-0" side="top">
        <Command.Root>
            {#if projects.length >= SEARCH_THRESHOLD}
                <Command.Input aria-label="Search projects" placeholder="Search projects…" />
            {/if}
            <Command.List class="max-h-72">
                <Command.Empty>No projects found.</Command.Empty>
                <Command.Group heading="Exie searches">
                    <Command.Item data-checked={!value} keywords={['all', 'organization']} onSelect={() => select(undefined)} value={ALL_PROJECTS}>
                        <Layers2 aria-hidden="true" />
                        <span>All projects</span>
                    </Command.Item>
                    {#each sortedProjects as project (project.id)}
                        <Command.Item data-checked={value === project.id} keywords={[project.name]} onSelect={() => select(project.id)} value={project.id}>
                            <Folder aria-hidden="true" />
                            <span class="min-w-0 truncate">{project.name}</span>
                        </Command.Item>
                    {/each}
                </Command.Group>
            </Command.List>
        </Command.Root>
    </Popover.Content>
</Popover.Root>
