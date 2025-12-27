import type { infer as Infer } from "zod";

import {
  array,
  boolean,
  email,
  int,
  int32,
  iso,
  lazy,
  literal,
  number,
  object,
  record,
  string,
  union,
  unknown,
  url,
  enum as zodEnum,
} from "zod";

export const StackStatusSchema = zodEnum([
  "open",
  "fixed",
  "regressed",
  "snoozed",
  "ignored",
  "discarded",
]);
export const BillingStatusSchema = union([
  literal(0),
  literal(1),
  literal(2),
  literal(3),
  literal(4),
]);

export const BillingPlanSchema = object({
  id: string().min(1, "Id is required"),
  name: string().min(1, "Name is required"),
  description: string().min(1, "Description is required"),
  price: number().optional(),
  max_projects: int32().optional(),
  max_users: int32().optional(),
  retention_days: int32().optional(),
  max_events_per_month: int32().optional(),
  has_premium_features: boolean().optional(),
  is_hidden: boolean().optional(),
});
export type BillingPlanFormData = Infer<typeof BillingPlanSchema>;

export const ChangePasswordModelSchema = object({
  current_password: string()
    .min(6, "Current password must be at least 6 characters")
    .max(100, "Current password must be at most 100 characters"),
  password: string()
    .min(6, "Password must be at least 6 characters")
    .max(100, "Password must be at most 100 characters"),
});
export type ChangePasswordModelFormData = Infer<
  typeof ChangePasswordModelSchema
>;

export const ChangePlanResultSchema = object({
  success: boolean().optional(),
  message: string().nullable().optional(),
});
export type ChangePlanResultFormData = Infer<typeof ChangePlanResultSchema>;

export const ClientConfigurationSchema = object({
  version: int32().optional(),
  settings: record(string(), string()).optional(),
});
export type ClientConfigurationFormData = Infer<
  typeof ClientConfigurationSchema
>;

export const CountResultSchema = object({
  total: int().optional(),
  aggregations: record(
    string(),
    lazy(() => IAggregateSchema),
  ).optional(),
  data: record(string(), unknown()).optional(),
});
export type CountResultFormData = Infer<typeof CountResultSchema>;

export const ExternalAuthInfoSchema = object({
  clientId: string().min(1, "Client id is required"),
  code: string().min(1, "Code is required"),
  redirectUri: url(),
  inviteToken: string().nullable().optional(),
});
export type ExternalAuthInfoFormData = Infer<typeof ExternalAuthInfoSchema>;

export const IAggregateSchema = object({
  data: record(string(), unknown()).optional(),
});
export type IAggregateFormData = Infer<typeof IAggregateSchema>;

export const InviteSchema = object({
  token: string().min(1, "Token is required"),
  email_address: email(),
  date_added: iso.datetime(),
});
export type InviteFormData = Infer<typeof InviteSchema>;

export const InvoiceSchema = object({
  id: string().min(1, "Id is required"),
  organization_id: string().min(1, "Organization id is required"),
  organization_name: string().min(1, "Organization name is required"),
  date: iso.datetime(),
  paid: boolean(),
  total: number(),
  items: array(lazy(() => InvoiceLineItemSchema)).optional(),
});
export type InvoiceFormData = Infer<typeof InvoiceSchema>;

export const InvoiceGridModelSchema = object({
  id: string().min(1, "Id is required"),
  date: iso.datetime(),
  paid: boolean(),
});
export type InvoiceGridModelFormData = Infer<typeof InvoiceGridModelSchema>;

export const InvoiceLineItemSchema = object({
  description: string().min(1, "Description is required"),
  date: iso.datetime().nullable().optional(),
  amount: number(),
});
export type InvoiceLineItemFormData = Infer<typeof InvoiceLineItemSchema>;

export const LoginSchema = object({
  email: email(),
  password: string()
    .min(6, "Password must be at least 6 characters")
    .max(100, "Password must be at most 100 characters"),
  invite_token: string()
    .length(40, "Invite token must be exactly 40 characters")
    .nullable()
    .optional(),
});
export type LoginFormData = Infer<typeof LoginSchema>;

export const NewOrganizationSchema = object({
  name: string().min(1, "Name is required"),
});
export type NewOrganizationFormData = Infer<typeof NewOrganizationSchema>;

export const NewProjectSchema = object({
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .optional(),
  name: string().min(1, "Name is required"),
  delete_bot_data_enabled: boolean().optional(),
});
export type NewProjectFormData = Infer<typeof NewProjectSchema>;

export const NewTokenSchema = object({
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .optional(),
  project_id: string()
    .length(24, "Project id must be exactly 24 characters")
    .optional(),
  default_project_id: string()
    .length(24, "Default project id must be exactly 24 characters")
    .nullable()
    .optional(),
  scopes: array(string()).optional(),
  expires_utc: iso.datetime().nullable().optional(),
  notes: string().nullable().optional(),
});
export type NewTokenFormData = Infer<typeof NewTokenSchema>;

export const NewWebHookSchema = object({
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .optional(),
  project_id: string()
    .length(24, "Project id must be exactly 24 characters")
    .optional(),
  url: url(),
  event_types: array(
    zodEnum([
      "CriticalError",
      "CriticalEvent",
      "NewError",
      "NewEvent",
      "StackPromoted",
      "StackRegression",
    ]),
  ),
  version: string().optional(),
});
export type NewWebHookFormData = Infer<typeof NewWebHookSchema>;

export const NotificationSettingsSchema = object({
  send_daily_summary: boolean().optional(),
  report_new_errors: boolean().optional(),
  report_critical_errors: boolean().optional(),
  report_event_regressions: boolean().optional(),
  report_new_events: boolean().optional(),
  report_critical_events: boolean().optional(),
});
export type NotificationSettingsFormData = Infer<
  typeof NotificationSettingsSchema
>;

export const OAuthAccountSchema = object({
  provider: string().min(1, "Provider is required"),
  provider_user_id: string().min(1, "Provider user id is required"),
  username: string().min(1, "Username is required"),
  extra_data: record(string(), string()).optional(),
});
export type OAuthAccountFormData = Infer<typeof OAuthAccountSchema>;

export const PersistentEventSchema = object({
  id: string().min(1, "Id is required").optional(),
  organization_id: string().min(1, "Organization id is required").optional(),
  project_id: string().min(1, "Project id is required").optional(),
  stack_id: string().min(1, "Stack id is required").optional(),
  is_first_occurrence: boolean().optional(),
  created_utc: iso.datetime().optional(),
  idx: record(string(), unknown()).optional(),
  type: string().nullable().optional(),
  source: string().nullable().optional(),
  date: iso.datetime().optional(),
  tags: array(string()).nullable().optional(),
  message: string().nullable().optional(),
  geo: string().nullable().optional(),
  value: number().nullable().optional(),
  count: int32().nullable().optional(),
  data: record(string(), unknown()).optional(),
  reference_id: string().nullable().optional(),
});
export type PersistentEventFormData = Infer<typeof PersistentEventSchema>;

export const ResetPasswordModelSchema = object({
  password_reset_token: string().length(
    40,
    "Password reset token must be exactly 40 characters",
  ),
  password: string()
    .min(6, "Password must be at least 6 characters")
    .max(100, "Password must be at most 100 characters"),
});
export type ResetPasswordModelFormData = Infer<typeof ResetPasswordModelSchema>;

export const SignupSchema = object({
  name: string().min(1, "Name is required"),
  email: email(),
  password: string()
    .min(6, "Password must be at least 6 characters")
    .max(100, "Password must be at most 100 characters"),
  invite_token: string()
    .length(40, "Invite token must be exactly 40 characters")
    .nullable()
    .optional(),
});
export type SignupFormData = Infer<typeof SignupSchema>;

export const StackSchema = object({
  id: string().length(24, "Id must be exactly 24 characters").optional(),
  organization_id: string().length(
    24,
    "Organization id must be exactly 24 characters",
  ),
  project_id: string().length(24, "Project id must be exactly 24 characters"),
  type: string()
    .min(1, "Type is required")
    .max(100, "Type must be at most 100 characters"),
  status: StackStatusSchema.optional(),
  snooze_until_utc: iso.datetime().nullable().optional(),
  signature_hash: string().min(1, "Signature hash is required"),
  signature_info: record(string(), string()),
  fixed_in_version: string().nullable().optional(),
  date_fixed: iso.datetime().nullable().optional(),
  title: string()
    .min(1, "Title is required")
    .max(1000, "Title must be at most 1000 characters")
    .optional(),
  total_occurrences: int32().optional(),
  first_occurrence: iso.datetime().optional(),
  last_occurrence: iso.datetime().optional(),
  description: string().nullable().optional(),
  occurrences_are_critical: boolean().optional(),
  references: array(string()).optional(),
  tags: array(string()).optional(),
  duplicate_signature: string()
    .min(1, "Duplicate signature is required")
    .optional(),
  created_utc: iso.datetime().optional(),
  updated_utc: iso.datetime().optional(),
  is_deleted: boolean().optional(),
  allow_notifications: boolean().optional(),
});
export type StackFormData = Infer<typeof StackSchema>;

export const StringStringValuesKeyValuePairSchema = object({
  key: string().nullable().optional(),
  value: array(string()).optional(),
});
export type StringStringValuesKeyValuePairFormData = Infer<
  typeof StringStringValuesKeyValuePairSchema
>;

export const StringValueFromBodySchema = object({
  value: string().nullable().optional(),
});
export type StringValueFromBodyFormData = Infer<
  typeof StringValueFromBodySchema
>;

export const TokenResultSchema = object({
  token: string().min(1, "Token is required"),
});
export type TokenResultFormData = Infer<typeof TokenResultSchema>;

export const UpdateEmailAddressResultSchema = object({
  is_verified: boolean(),
});
export type UpdateEmailAddressResultFormData = Infer<
  typeof UpdateEmailAddressResultSchema
>;

export const UpdateProjectSchema = object({
  name: string().min(1, "Name is required"),
  delete_bot_data_enabled: boolean().optional(),
});
export type UpdateProjectFormData = Infer<typeof UpdateProjectSchema>;

export const UpdateTokenSchema = object({
  is_disabled: boolean().optional(),
  notes: string().nullable().optional(),
});
export type UpdateTokenFormData = Infer<typeof UpdateTokenSchema>;

export const UpdateUserSchema = object({
  full_name: string().min(1, "Full name is required"),
  email_notifications_enabled: boolean().optional(),
});
export type UpdateUserFormData = Infer<typeof UpdateUserSchema>;

export const UsageHourInfoSchema = object({
  date: iso.datetime().optional(),
  total: int32().optional(),
  blocked: int32().optional(),
  discarded: int32().optional(),
  too_big: int32().optional(),
});
export type UsageHourInfoFormData = Infer<typeof UsageHourInfoSchema>;

export const UsageInfoSchema = object({
  date: iso.datetime().optional(),
  limit: int32().optional(),
  total: int32().optional(),
  blocked: int32().optional(),
  discarded: int32().optional(),
  too_big: int32().optional(),
});
export type UsageInfoFormData = Infer<typeof UsageInfoSchema>;

export const UserSchema = object({
  id: string().min(1, "Id is required").optional(),
  organization_ids: array(string()).optional(),
  password: string().nullable().optional(),
  salt: string().nullable().optional(),
  password_reset_token: string().nullable().optional(),
  password_reset_token_expiration: iso.datetime().optional(),
  o_auth_accounts: array(lazy(() => OAuthAccountSchema)).optional(),
  full_name: string().min(1, "Full name is required"),
  email_address: email(),
  email_notifications_enabled: boolean().optional(),
  is_email_address_verified: boolean().optional(),
  verify_email_address_token: string().nullable().optional(),
  verify_email_address_token_expiration: iso.datetime().optional(),
  is_active: boolean().optional(),
  roles: array(string()).optional(),
  created_utc: iso.datetime().optional(),
  updated_utc: iso.datetime().optional(),
});
export type UserFormData = Infer<typeof UserSchema>;

export const UserDescriptionSchema = object({
  email_address: email().nullable().optional(),
  description: string().min(1, "Description is required"),
  data: record(string(), unknown()).optional(),
});
export type UserDescriptionFormData = Infer<typeof UserDescriptionSchema>;

export const ViewCurrentUserSchema = object({
  hash: string().nullable().optional(),
  has_local_account: boolean().optional(),
  o_auth_accounts: array(lazy(() => OAuthAccountSchema)).optional(),
  id: string().min(1, "Id is required").optional(),
  organization_ids: array(string()).optional(),
  full_name: string().min(1, "Full name is required").optional(),
  email_address: email().optional(),
  email_notifications_enabled: boolean().optional(),
  is_email_address_verified: boolean().optional(),
  is_active: boolean().optional(),
  is_invite: boolean().optional(),
  roles: array(string()).optional(),
});
export type ViewCurrentUserFormData = Infer<typeof ViewCurrentUserSchema>;

export const ViewOrganizationSchema = object({
  id: string().min(1, "Id is required").optional(),
  created_utc: iso.datetime().optional(),
  updated_utc: iso.datetime().optional(),
  name: string().min(1, "Name is required").optional(),
  plan_id: string().min(1, "Plan id is required").optional(),
  plan_name: string().min(1, "Plan name is required").optional(),
  plan_description: string().min(1, "Plan description is required").optional(),
  card_last4: string().nullable().optional(),
  subscribe_date: iso.datetime().nullable().optional(),
  billing_change_date: iso.datetime().nullable().optional(),
  billing_changed_by_user_id: string().nullable().optional(),
  billing_status: BillingStatusSchema.optional(),
  billing_price: number().optional(),
  max_events_per_month: int32().optional(),
  bonus_events_per_month: int32().optional(),
  bonus_expiration: iso.datetime().nullable().optional(),
  retention_days: int32().optional(),
  is_suspended: boolean().optional(),
  suspension_code: string().nullable().optional(),
  suspension_notes: string().nullable().optional(),
  suspension_date: iso.datetime().nullable().optional(),
  has_premium_features: boolean().optional(),
  max_users: int32().optional(),
  max_projects: int32().optional(),
  project_count: int().optional(),
  stack_count: int().optional(),
  event_count: int().optional(),
  invites: array(lazy(() => InviteSchema)).optional(),
  usage_hours: array(lazy(() => UsageHourInfoSchema)).optional(),
  usage: array(lazy(() => UsageInfoSchema)).optional(),
  data: record(string(), unknown()).optional(),
  is_throttled: boolean().optional(),
  is_over_monthly_limit: boolean().optional(),
  is_over_request_limit: boolean().optional(),
});
export type ViewOrganizationFormData = Infer<typeof ViewOrganizationSchema>;

export const ViewProjectSchema = object({
  id: string().min(1, "Id is required").optional(),
  created_utc: iso.datetime().optional(),
  organization_id: string().min(1, "Organization id is required").optional(),
  organization_name: string()
    .min(1, "Organization name is required")
    .optional(),
  name: string().min(1, "Name is required").optional(),
  delete_bot_data_enabled: boolean().optional(),
  data: record(string(), unknown()).optional(),
  promoted_tabs: array(string()).optional(),
  is_configured: boolean().nullable().optional(),
  stack_count: int().optional(),
  event_count: int().optional(),
  has_premium_features: boolean().optional(),
  has_slack_integration: boolean().optional(),
  usage_hours: array(lazy(() => UsageHourInfoSchema)).optional(),
  usage: array(lazy(() => UsageInfoSchema)).optional(),
});
export type ViewProjectFormData = Infer<typeof ViewProjectSchema>;

export const ViewTokenSchema = object({
  id: string().min(1, "Id is required").optional(),
  organization_id: string().min(1, "Organization id is required").optional(),
  project_id: string().min(1, "Project id is required").optional(),
  user_id: string().nullable().optional(),
  default_project_id: string().nullable().optional(),
  scopes: array(string()).optional(),
  expires_utc: iso.datetime().nullable().optional(),
  notes: string().nullable().optional(),
  is_disabled: boolean().optional(),
  is_suspended: boolean().optional(),
  created_utc: iso.datetime().optional(),
  updated_utc: iso.datetime().optional(),
});
export type ViewTokenFormData = Infer<typeof ViewTokenSchema>;

export const ViewUserSchema = object({
  id: string().min(1, "Id is required").optional(),
  organization_ids: array(string()).optional(),
  full_name: string().min(1, "Full name is required").optional(),
  email_address: email().optional(),
  email_notifications_enabled: boolean().optional(),
  is_email_address_verified: boolean().optional(),
  is_active: boolean().optional(),
  is_invite: boolean().optional(),
  roles: array(string()).optional(),
});
export type ViewUserFormData = Infer<typeof ViewUserSchema>;

export const WebHookSchema = object({
  id: string().min(1, "Id is required").optional(),
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .optional(),
  project_id: string()
    .length(24, "Project id must be exactly 24 characters")
    .optional(),
  url: url(),
  event_types: array(
    zodEnum([
      "CriticalError",
      "CriticalEvent",
      "NewError",
      "NewEvent",
      "StackPromoted",
      "StackRegression",
    ]),
  ),
  is_enabled: boolean().optional(),
  version: string().min(1, "Version is required"),
  created_utc: iso.datetime().optional(),
});
export type WebHookFormData = Infer<typeof WebHookSchema>;

export const WorkInProgressResultSchema = object({
  workers: array(string()).optional(),
});
export type WorkInProgressResultFormData = Infer<
  typeof WorkInProgressResultSchema
>;
