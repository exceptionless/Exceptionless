import type { NavigationItem } from '../../routes.svelte';

import { routes as projectSettingsRoutes } from './[projectId]/routes.svelte';

export function routes(): NavigationItem[] {
    return projectSettingsRoutes();
}
