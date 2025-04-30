import { IsDefined, IsOptional } from 'class-validator';

export { NewToken, ViewToken } from '$generated/api';

// TODO: Figure out open api gen.
export class UpdateToken {
    @IsDefined({ message: 'is_disabled is required.' }) is_disabled!: boolean;
    @IsOptional() notes?: null | string;
}
