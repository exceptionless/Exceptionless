export type { Invoice, InvoiceGridModel, NewOrganization, ViewOrganization } from '$generated/api';

// TODO: This should be generated from the backend enum - investigate why it wasn't included in the generated API
export enum SuspensionCode {
    Billing = 0,
    Overage = 1,
    Abuse = 2,
    Other = 100
}

export class SetBonusOrganizationForm {
    @IsInt()
    bonusEvents!: number;

    @IsDate()
    @IsOptional()
    expires?: Date;
}

export class SuspendOrganizationForm {
    @IsEnum(SuspensionCode)
    code!: SuspensionCode;

    @IsOptional()
    @IsString()
    notes?: string;
}
