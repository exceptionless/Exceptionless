import { type infer as Infer, object, string } from 'zod';

export const CustomDateRangeSchema = object({
    end: string().optional(),
    start: string().optional()
});
export type CustomDateRangeFormData = Infer<typeof CustomDateRangeSchema>;
