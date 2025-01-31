import type { IFilter } from '$comp/faceted-filter';

import { organization } from '$features/organizations/context.svelte';

import { getKeywordFilter, getProjectFilter, getStackFilter } from './models.svelte';

export function shouldRefreshPersistentEventChanged(
    filters: IFilter[],
    filter: string,
    organization_id?: string,
    project_id?: string,
    stack_id?: string,
    id?: string
) {
    if (!filter) {
        return true;
    }

    if (id) {
        // This could match any kind of lucene query (even must not filtering)
        const keywordFilter = getKeywordFilter(filters);
        if (keywordFilter && !keywordFilter.isEmpty()) {
            if (keywordFilter.value!.includes(id)) {
                return true;
            }
        }
    }

    if (stack_id) {
        const stackFilter = getStackFilter(filters);
        if (stackFilter && !stackFilter.isEmpty()) {
            return stackFilter.value === stack_id;
        }
    }

    if (project_id) {
        const projectFilter = getProjectFilter(filters);
        if (projectFilter && !projectFilter.isEmpty()) {
            return projectFilter.value.includes(project_id);
        }
    }

    if (organization_id) {
        return organization.current === organization_id;
    }

    return true;
}
