import type { DiscardedEnvironment, ProjectEnvironment } from '#generated/api.ts';

/** Environment names are identifiers: 1 to 64 ASCII letters, digits, periods, underscores, or hyphens, starting with a letter or digit. */
export const ENVIRONMENT_NAME_PATTERN = /^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$/;
export const MAX_LISTED_ENVIRONMENTS = 100;

export type DiscardUnlistedWarning = 'empty-list' | 'no-recent-events';

export interface EnvironmentRow {
    count: number;
    isListed: boolean;
    key: string;
    lastSeen?: string;
    /** The listed casing, otherwise the aggregation key. */
    name: string;
    /** Only set for listed environments. Unlisted ones follow the project's other-environments setting. */
    notify?: boolean;
}

export interface ObservedEnvironment {
    count: number;
    key: string;
    lastSeen?: string;
}

interface ProjectWithEnvironments {
    environments?: null | ProjectEnvironment[];
    id: string;
}

/**
 * Combines the listed environments with the ones seen in event data. Listed names keep their stored casing and come first;
 * names that are only observed use the lowercase aggregation key and are ordered by how many events they have.
 */
export function buildEnvironmentRows(listed: null | ProjectEnvironment[] | undefined, observed: ObservedEnvironment[]): EnvironmentRow[] {
    const observedByKey = new Map(observed.map((environment) => [getEnvironmentKey(environment.key), environment]));
    const listedKeys = new Set<string>();
    const listedRows = (listed ?? [])
        .map((environment) => {
            const key = getEnvironmentKey(environment.name);
            listedKeys.add(key);
            const seen = observedByKey.get(key);

            return {
                count: seen?.count ?? 0,
                isListed: true,
                key,
                lastSeen: seen?.lastSeen,
                name: environment.name,
                notify: environment.notify
            } satisfies EnvironmentRow;
        })
        .sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }) || a.name.localeCompare(b.name));

    const unlistedRows = observed
        .filter((environment) => !listedKeys.has(getEnvironmentKey(environment.key)))
        .map((environment) => ({
            count: environment.count,
            isListed: false,
            key: getEnvironmentKey(environment.key),
            lastSeen: environment.lastSeen,
            name: environment.key
        }))
        .sort((a, b) => b.count - a.count || a.key.localeCompare(b.key));

    return [...listedRows, ...unlistedRows];
}

/**
 * Discarding unlisted environments drops every event whose environment is not in the list. That is almost never intended when the list
 * is empty, or when none of the listed environments has sent events recently, so the page asks for confirmation first.
 * Recent activity is only judged once it has loaded.
 */
export function getDiscardUnlistedWarning(rows: EnvironmentRow[], observedLoaded: boolean): DiscardUnlistedWarning | undefined {
    const listed = rows.filter((row) => row.isListed);
    if (listed.length === 0) {
        return 'empty-list';
    }

    return observedLoaded && listed.every((row) => row.count === 0) ? 'no-recent-events' : undefined;
}

/** Returns the listed casing for a listed environment, otherwise the name as it was given. */
export function getEnvironmentDisplayName(environments: null | ProjectEnvironment[] | undefined, name: string): string {
    const key = getEnvironmentKey(name);
    return environments?.find((environment) => getEnvironmentKey(environment.name) === key)?.name ?? name;
}

/** The case-insensitive key for a name. Event aggregations return lowercase keys. */
export function getEnvironmentKey(name: string): string {
    return name.toLowerCase();
}

/**
 * Maps environment keys to their listed casing. When projects disagree about the casing, the first project wins.
 * Pass the project ids a view is limited to so unrelated projects do not decide the casing.
 */
export function getListedEnvironmentNames(projects: ProjectWithEnvironments[], projectIds?: string[]): Map<string, string> {
    const names = new Map<string, string>();
    for (const project of projects) {
        if (projectIds?.length && !projectIds.includes(project.id)) {
            continue;
        }

        for (const environment of project.environments ?? []) {
            const key = getEnvironmentKey(environment.name);
            if (!names.has(key)) {
                names.set(key, environment.name);
            }
        }
    }

    return names;
}

/** Applies the same rules as the server: the trimmed name, or undefined when it is not a valid identifier. */
export function normalizeEnvironmentName(value: null | string | undefined): string | undefined {
    const name = value?.trim();
    return name && ENVIRONMENT_NAME_PATTERN.test(name) ? name : undefined;
}

/** Discarded names with a name, most discarded first. The bucket without an environment has no name. */
export function splitDiscardedEnvironments(discarded: DiscardedEnvironment[] | null | undefined): {
    named: DiscardedEnvironment[];
    unspecified?: DiscardedEnvironment;
} {
    const all = discarded ?? [];
    return {
        named: all.filter((environment) => !!environment.key).sort((a, b) => b.count - a.count),
        unspecified: all.find((environment) => !environment.key)
    };
}
