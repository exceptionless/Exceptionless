import { date, type infer as Infer, object, string } from 'zod';

export const RunMaintenanceJobSchema = object({
    confirmText: string().min(1),
    organizationId: string().optional(),
    utcEnd: date().optional(),
    utcStart: date().optional()
});
export type RunMaintenanceJobFormData = Infer<typeof RunMaintenanceJobSchema>;
