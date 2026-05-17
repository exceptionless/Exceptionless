export interface OrganizationFeatureDefinition {
    description: string;
    id: string;
    name: string;
}

// Keep this list empty unless there are active, organization-scoped feature toggles.
export const organizationFeatureDefinitions: OrganizationFeatureDefinition[] = [];
