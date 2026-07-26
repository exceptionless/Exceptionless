export type { ClientConfiguration, NewProject, NotificationSettings, UpdateProject, ViewProject } from '$generated/api';

export interface ClientConfigurationSetting {
    key: string;
    value: string;
}

export interface SourceMapArtifact {
    created_utc: string;
    file_name?: string;
    generated_file_url: string;
    id: string;
    is_auto_downloaded: boolean;
    last_used_utc?: string;
    size: number;
    source_map_url?: string;
}
