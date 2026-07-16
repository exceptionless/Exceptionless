import type { UpdateProject as GeneratedUpdateProject } from '$generated/api';

export { ProjectIngestLimitType } from '$generated/api';
export type { ClientConfiguration, NewProject, NotificationSettings, ProjectIngestLimit, ViewProject } from '$generated/api';
export interface ClientConfigurationSetting {
    key: string;
    value: string;
}

export type UpdateProject = Partial<GeneratedUpdateProject>;
