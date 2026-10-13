<script module lang="ts">
    export type QueryGate = 'assistant-sharing' | 'project-commands';
</script>

<script lang="ts">
    import { getAssistantConversationSharingQuery } from '$features/assistant/api.svelte';
    import { getOrganizationProjectsQuery } from '$features/projects/api.svelte';
    import { untrack } from 'svelte';

    let { enabled, gate }: { enabled: boolean; gate: QueryGate } = $props();
    const query =
        untrack(() => gate) === 'project-commands'
            ? getOrganizationProjectsQuery({
                  enabled: () => enabled,
                  route: {
                      organizationId: 'organization-id'
                  }
              })
            : getAssistantConversationSharingQuery({
                  get enabled() {
                      return enabled;
                  }
              });
</script>

<output>{JSON.stringify(query.data)}</output>
