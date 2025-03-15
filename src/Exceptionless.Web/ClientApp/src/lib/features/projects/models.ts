export { NewProject, ViewProject } from '$generated/api';

import { IsBoolean, IsString } from 'class-validator';

export class UpdateProject {
    @IsBoolean({ message: 'delete_bot_data_enabled is required.' }) delete_bot_data_enabled: boolean = true;
    @IsString({ message: 'name is required.' }) name!: string;
}
