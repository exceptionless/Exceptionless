export type { ClientConfiguration, NewProject, NotificationSettings, ViewProject } from '$generated/api';

import { IsBoolean, IsOptional, IsString } from 'class-validator';

export class ClientConfigurationSetting {
    @IsString({ message: 'key is required.' })
    key!: string;
    @IsString({ message: 'value is required.' })
    value!: string;
}

export class UpdateProject {
    @IsBoolean({ message: 'delete_bot_data_enabled is required.' })
    @IsOptional()
    delete_bot_data_enabled: boolean = true;
    @IsOptional()
    @IsString({ message: 'name is required.' })
    name!: string;
}
