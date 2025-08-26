<script lang="ts">
    interface Props {
        includeDateFacets?: boolean;
    }

    const { includeDateFacets = true }: Props = $props();
    import * as FacetedFilter from './index';

    type KnownTermsFilterConfig = {
        priority?: number;
        term: string;
        title: string;
    };

    const eventsBooleanFilters: KnownTermsFilterConfig[] = [
        { term: 'first', title: 'First Occurrence' },
        { term: 'bot', title: 'Is Bot' }
    ];

    const eventsDateFilters: KnownTermsFilterConfig[] = [{ priority: 50, term: 'date', title: 'Date' }];

    const eventsGeoFilters: KnownTermsFilterConfig[] = [{ term: 'geo', title: 'Location' }];

    const eventsNumberFilters: KnownTermsFilterConfig[] = [
        { term: 'value', title: 'Value' },
        { term: 'count', title: 'Count' },
        { term: 'data.@request.port', title: 'Http Port' } // TODO: verify
    ];

    const eventsStringFilters: KnownTermsFilterConfig[] = [
        { term: 'stack', title: 'Stack Id' }, // TODO: Think about if this needed
        { term: 'id', title: 'Id' }, // TODO: Think about if this needed
        { term: 'source', title: 'Source' },
        { priority: 80, term: 'message', title: 'Message' },
        { term: 'submission', title: 'Submission Method' },
        { term: 'ip', title: 'Ip Address' },
        { term: 'useragent', title: 'User Agent' },
        { priority: 40, term: 'path', title: 'Http Path' },
        { term: 'data.@request.host', title: 'Http Host' }, // TODO: verify
        { term: 'data.@request.http_method', title: 'Http Method' }, // TODO: verify
        { term: 'browser', title: 'Browser' },
        { term: 'client.useragent', title: 'Client User Agent' },
        { term: 'device', title: 'Device' },
        { term: 'os', title: 'OS' },
        { term: 'cmd', title: 'Command Line' },
        { term: 'machine', title: 'Machine Name' },
        { term: 'architecture', title: 'Machine Architecture' },
        { priority: 50, term: 'user', title: 'User' },
        { term: 'user.name', title: 'User Name' },
        { term: 'user.email', title: 'User Email' },
        { term: 'user.description', title: 'User Description' },
        { term: 'country', title: 'Country' },
        { term: 'level1', title: 'Region' },
        { term: 'level2', title: 'City' },
        { term: 'locality', title: 'Locality' },
        { term: 'error.code', title: 'Error Code' },
        { term: 'error.type', title: 'Error Type' },
        { term: 'error.message', title: 'Error Message' },
        { term: 'error.targettype', title: 'Error Target Type' },
        { term: 'error.targetmethod', title: 'Error Target Method' }
    ];

    const eventsVersionFilters: KnownTermsFilterConfig[] = [
        { term: 'version', title: 'Version' },
        { term: 'browser.version', title: 'Browser Version' },
        { term: 'browser.major', title: 'Browser Major Version' },
        { term: 'client.version', title: 'Client Version' },
        { term: 'os.version', title: 'OS Version' },
        { term: 'os.major', title: 'OS Major Version' }
    ];
</script>

<FacetedFilter.KeywordBuilder priority={15} />
<FacetedFilter.LevelBuilder priority={50} />

{#each eventsBooleanFilters as { priority, term, title } (term)}
    <FacetedFilter.BooleanBuilder {priority} {term} {title} />
{/each}

{#if includeDateFacets}
    {#each eventsDateFilters as { priority, term, title } (term)}
        <FacetedFilter.DateBuilder {priority} {term} {title} />
    {/each}
{/if}

{#each eventsGeoFilters as { priority, term, title } (term)}
    <FacetedFilter.StringBuilder {priority} {term} {title} />
{/each}

{#each eventsNumberFilters as { priority, term, title } (term)}
    <FacetedFilter.NumberBuilder {priority} {term} {title} />
{/each}

<FacetedFilter.ProjectBuilder priority={10} />
<FacetedFilter.ReferenceBuilder />
<FacetedFilter.SessionBuilder />
<FacetedFilter.StatusBuilder priority={50} />

{#each eventsStringFilters as { priority, term, title } (term)}
    <FacetedFilter.StringBuilder {priority} {term} {title} />
{/each}

<FacetedFilter.TagBuilder priority={70} />
<FacetedFilter.TypeBuilder priority={50} />

{#each eventsVersionFilters as { priority, term, title } (term)}
    <FacetedFilter.VersionBuilder {priority} {term} {title} />
{/each}
