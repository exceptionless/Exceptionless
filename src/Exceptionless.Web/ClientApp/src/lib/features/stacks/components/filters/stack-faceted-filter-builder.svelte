<script lang="ts">
    import * as FacetedFilter from '$features/events/components/filters';
    import { STACK_CRITICAL_TERM } from '$features/stacks/stack-filter-support';

    interface Props {
        includeProject?: boolean;
    }

    let { includeProject = true }: Props = $props();
</script>

<FacetedFilter.KeywordBuilder priority={15} />
{#if includeProject}
    <FacetedFilter.ProjectBuilder priority={10} />
{/if}
<FacetedFilter.StringBuilder priority={16} term="stack" title="Stack Id" />
<FacetedFilter.StatusBuilder priority={20} />
<FacetedFilter.TypeBuilder priority={30} />
<FacetedFilter.TagBuilder priority={40} />
<FacetedFilter.BooleanBuilder priority={45} term={STACK_CRITICAL_TERM} title="Critical" />
<FacetedFilter.StringBuilder priority={50} term="title" title="Title" />
<FacetedFilter.StringBuilder priority={51} term="description" title="Description" />
<FacetedFilter.StringBuilder priority={52} term="links" title="Reference" />
<FacetedFilter.NumberBuilder priority={53} term="occurrences" title="Total Occurrences" />
<FacetedFilter.StringBuilder priority={54} term="version_fixed" title="Fixed In Version" />
<FacetedFilter.DateBuilder priority={55} term="fixedon" title="Date Fixed" />
<FacetedFilter.DateBuilder priority={56} term="first" title="First Occurrence" />
<FacetedFilter.DateBuilder priority={57} term="last" title="Last Occurrence" />
<FacetedFilter.DateBuilder priority={58} term="snooze_until_utc" title="Snooze Until" />
