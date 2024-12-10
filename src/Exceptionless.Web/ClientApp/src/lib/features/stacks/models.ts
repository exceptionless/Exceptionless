export { Stack, StackStatus } from '$generated/api';
import { IsUrl } from 'class-validator';

export class ReferenceLinkForm {
    @IsUrl({ require_tld: false })
    url!: string;
}
