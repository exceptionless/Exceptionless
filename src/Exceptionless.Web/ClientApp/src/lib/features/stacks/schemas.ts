import { type infer as Infer, object, string, url } from 'zod';

// SemVer regex pattern for validation
const semverPattern =
    /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$/;

// Custom schema with semver refinement (not in API)
export const FixedInVersionSchema = object({
    version: string()
        .refine((val) => !val || semverPattern.test(val), {
            message: 'Version must be a valid semantic version (e.g., 1.0.0)'
        })
        .optional()
});
export type FixedInVersionFormData = Infer<typeof FixedInVersionSchema>;

// Custom schema for reference links (not in API)
export const ReferenceLinkSchema = object({
    url: url('Please enter a valid URL')
});
export type ReferenceLinkFormData = Infer<typeof ReferenceLinkSchema>;
