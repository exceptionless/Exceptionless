import { type infer as Infer, object, string, enum as zodEnum } from 'zod';

export const ChangePlanSchema = object({
    cardMode: zodEnum(['existing', 'new']),
    couponId: string(),
    selectedPlanId: string().min(1, 'Please select a plan.')
});

export type ChangePlanFormData = Infer<typeof ChangePlanSchema>;
