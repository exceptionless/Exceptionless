export { ProjectIngestLimitType } from '$generated/api';
export type { ClientConfiguration, NewProject, NotificationSettings, UpdateProject, ViewProject } from '$generated/api';

export interface ClientConfigurationSetting {
    key: string;
    value: string;
}

export interface ProjectIngestLimitUpdate {
    type: 'fixed' | 'percent';
    fixed_limit?: number | null;
    percent_of_organization_limit?: number | null;
}
