import { date, type infer as Infer, number, object, string, enum as zodEnum } from 'zod';

import { SuspensionCode } from './models';

export { type NewOrganizationFormData, NewOrganizationSchema } from '$generated/schemas';

export const OrganizationBillingInformationSchema = object({
    address: string(),
    name: string(),
    vatId: string(),
    vatNumber: string()
});
export type OrganizationBillingInformationFormData = Infer<typeof OrganizationBillingInformationSchema>;

export const SetBonusOrganizationSchema = object({
    bonusEvents: number().int('Bonus events must be a whole number'),
    expires: date().optional()
});
export type SetBonusOrganizationFormData = Infer<typeof SetBonusOrganizationSchema>;

export const SuspendOrganizationSchema = object({
    code: zodEnum(SuspensionCode, { message: 'Suspension code is required' }),
    notes: string().optional()
});
export type SuspendOrganizationFormData = Infer<typeof SuspendOrganizationSchema>;
