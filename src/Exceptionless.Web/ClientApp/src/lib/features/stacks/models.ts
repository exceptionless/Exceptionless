export { Stack, StackStatus } from '$generated/api';
import { IsOptional, IsSemVer, IsUrl } from 'class-validator';

export class FixedInVersionForm {
    @IsOptional()
    @IsSemVer()
    version?: string;
}

export class ReferenceLinkForm {
    @IsUrl({ require_tld: false })
    url!: string;
}
