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
  price: number(),
  max_projects: int32(),
  max_users: int32(),
  retention_days: int32(),
  max_events_per_month: int32(),
  has_premium_features: boolean(),
  is_hidden: boolean(),
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
  success: boolean(),
  message: string().min(1, "Message is required").nullable().optional(),
});
export type ChangePlanResultFormData = Infer<typeof ChangePlanResultSchema>;

export const ClientConfigurationSchema = object({
  version: int32(),
  settings: record(string(), string()),
});
export type ClientConfigurationFormData = Infer<
  typeof ClientConfigurationSchema
>;

export const CountResultSchema = object({
  total: int(),
  aggregations: record(
    string(),
    lazy(() => IAggregateSchema),
  ).optional(),
  data: record(string(), unknown()).nullable().optional(),
});
export type CountResultFormData = Infer<typeof CountResultSchema>;

export const ExternalAuthInfoSchema = object({
  clientId: string().min(1, "Client id is required"),
  code: string().min(1, "Code is required"),
  redirectUri: url(),
  inviteToken: string()
    .min(1, "Invite token is required")
    .nullable()
    .optional(),
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
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Organization id has invalid format"),
  organization_name: string().min(1, "Organization name is required"),
  date: iso.datetime(),
  paid: boolean(),
  total: number(),
  items: array(lazy(() => InvoiceLineItemSchema)),
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
    .regex(/^[a-fA-F0-9]{24}$/, "Organization id has invalid format"),
  name: string().min(1, "Name is required"),
  delete_bot_data_enabled: boolean(),
});
export type NewProjectFormData = Infer<typeof NewProjectSchema>;

export const NewTokenSchema = object({
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Organization id has invalid format")
    .nullable()
    .optional(),
  project_id: string()
    .length(24, "Project id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Project id has invalid format")
    .nullable()
    .optional(),
  default_project_id: string()
    .length(24, "Default project id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Default project id has invalid format")
    .nullable()
    .optional(),
  scopes: array(string()),
  expires_utc: iso.datetime().nullable().optional(),
  notes: string().min(1, "Notes is required").nullable().optional(),
});
export type NewTokenFormData = Infer<typeof NewTokenSchema>;

export const NewWebHookSchema = object({
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Organization id has invalid format"),
  project_id: string()
    .length(24, "Project id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Project id has invalid format"),
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
  version: string()
    .regex(/^\d+(\.\d+){1,3}$/, "Version has invalid format")
    .nullable()
    .optional(),
});
export type NewWebHookFormData = Infer<typeof NewWebHookSchema>;

export const NotificationSettingsSchema = object({
  send_daily_summary: boolean(),
  report_new_errors: boolean(),
  report_critical_errors: boolean(),
  report_event_regressions: boolean(),
  report_new_events: boolean(),
  report_critical_events: boolean(),
});
export type NotificationSettingsFormData = Infer<
  typeof NotificationSettingsSchema
>;

export const OAuthAccountSchema = object({
  provider: string().min(1, "Provider is required"),
  provider_user_id: string().min(1, "Provider user id is required"),
  username: string().min(1, "Username is required"),
  extra_data: record(string(), unknown()).nullable().optional(),
});
export type OAuthAccountFormData = Infer<typeof OAuthAccountSchema>;

export const PersistentEventSchema = object({
  id: string()
    .length(24, "Id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Id has invalid format"),
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Organization id has invalid format"),
  project_id: string()
    .length(24, "Project id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Project id has invalid format"),
  stack_id: string()
    .length(24, "Stack id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Stack id has invalid format"),
  is_first_occurrence: boolean(),
  created_utc: iso.datetime(),
  idx: record(string(), unknown()),
  type: string()
    .min(1, "Type is required")
    .max(100, "Type must be at most 100 characters")
    .nullable()
    .optional(),
  source: string()
    .min(1, "Source is required")
    .max(2000, "Source must be at most 2000 characters")
    .nullable()
    .optional(),
  date: iso.datetime(),
  tags: array(string()).nullable().optional(),
  message: string()
    .min(1, "Message is required")
    .max(2000, "Message must be at most 2000 characters")
    .nullable()
    .optional(),
  geo: string().min(1, "Geo is required").nullable().optional(),
  value: number().nullable().optional(),
  count: int32().nullable().optional(),
  data: record(string(), unknown()).nullable().optional(),
  reference_id: string()
    .min(1, "Reference id is required")
    .nullable()
    .optional(),
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
  id: string()
    .length(24, "Id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Id has invalid format"),
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Organization id has invalid format"),
  project_id: string()
    .length(24, "Project id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Project id has invalid format"),
  type: string()
    .min(1, "Type is required")
    .max(100, "Type must be at most 100 characters"),
  status: StackStatusSchema,
  snooze_until_utc: iso.datetime().nullable().optional(),
  signature_hash: string().min(1, "Signature hash is required"),
  signature_info: record(string(), string()),
  fixed_in_version: string().nullable().optional(),
  date_fixed: iso.datetime().nullable().optional(),
  title: string()
    .min(1, "Title is required")
    .max(1000, "Title must be at most 1000 characters"),
  total_occurrences: int32(),
  first_occurrence: iso.datetime(),
  last_occurrence: iso.datetime(),
  description: string().min(1, "Description is required").nullable().optional(),
  occurrences_are_critical: boolean(),
  references: array(string()),
  tags: array(string()),
  duplicate_signature: string().min(1, "Duplicate signature is required"),
  created_utc: iso.datetime(),
  updated_utc: iso.datetime(),
  is_deleted: boolean(),
  allow_notifications: boolean(),
});
export type StackFormData = Infer<typeof StackSchema>;

export const StringStringValuesKeyValuePairSchema = object({
  key: string().min(1, "Key is required").nullable(),
  value: array(string()),
});
export type StringStringValuesKeyValuePairFormData = Infer<
  typeof StringStringValuesKeyValuePairSchema
>;

export const StringValueFromBodySchema = object({
  value: string().min(1, "Value is required").nullable(),
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

export const UpdateEventSchema = object({
  email_address: email().nullable().optional(),
  description: string().min(1, "Description is required").nullable().optional(),
});
export type UpdateEventFormData = Infer<typeof UpdateEventSchema>;

export const UpdateProjectSchema = object({
  name: string().min(1, "Name is required").optional(),
  delete_bot_data_enabled: boolean().optional(),
});
export type UpdateProjectFormData = Infer<typeof UpdateProjectSchema>;

export const UpdateTokenSchema = object({
  is_disabled: boolean().optional(),
  notes: string().min(1, "Notes is required").nullable().optional(),
});
export type UpdateTokenFormData = Infer<typeof UpdateTokenSchema>;

export const UpdateUserSchema = object({
  full_name: string().min(1, "Full name is required").optional(),
  email_notifications_enabled: boolean().optional(),
});
export type UpdateUserFormData = Infer<typeof UpdateUserSchema>;

export const UsageHourInfoSchema = object({
  date: iso.datetime(),
  total: int32(),
  blocked: int32(),
  discarded: int32(),
  too_big: int32(),
});
export type UsageHourInfoFormData = Infer<typeof UsageHourInfoSchema>;

export const UsageInfoSchema = object({
  date: iso.datetime(),
  limit: int32(),
  total: int32(),
  blocked: int32(),
  discarded: int32(),
  too_big: int32(),
});
export type UsageInfoFormData = Infer<typeof UsageInfoSchema>;

export const UserSchema = object({
  id: string()
    .length(24, "Id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Id has invalid format"),
  organization_ids: array(string()).optional(),
  password: string().min(1, "Password is required").nullable().optional(),
  salt: string().min(1, "Salt is required").nullable().optional(),
  password_reset_token: string()
    .min(1, "Password reset token is required")
    .nullable()
    .optional(),
  password_reset_token_expiration: iso.datetime(),
  o_auth_accounts: array(lazy(() => OAuthAccountSchema)).optional(),
  full_name: string().min(1, "Full name is required"),
  email_address: email(),
  email_notifications_enabled: boolean(),
  is_email_address_verified: boolean(),
  verify_email_address_token: string()
    .min(1, "Verify email address token is required")
    .nullable()
    .optional(),
  verify_email_address_token_expiration: iso.datetime(),
  is_active: boolean(),
  roles: array(string()),
  created_utc: iso.datetime(),
  updated_utc: iso.datetime(),
});
export type UserFormData = Infer<typeof UserSchema>;

export const UserDescriptionSchema = object({
  email_address: email().nullable().optional(),
  description: string().min(1, "Description is required").nullable().optional(),
  data: record(string(), unknown()).nullable().optional(),
});
export type UserDescriptionFormData = Infer<typeof UserDescriptionSchema>;

export const ViewCurrentUserSchema = object({
  hash: string().min(1, "Hash is required").nullable().optional(),
  has_local_account: boolean(),
  o_auth_accounts: array(lazy(() => OAuthAccountSchema)),
  id: string()
    .length(24, "Id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Id has invalid format"),
  organization_ids: array(string()),
  full_name: string().min(1, "Full name is required"),
  email_address: email(),
  email_notifications_enabled: boolean(),
  is_email_address_verified: boolean(),
  is_active: boolean(),
  is_invite: boolean(),
  roles: array(string()),
});
export type ViewCurrentUserFormData = Infer<typeof ViewCurrentUserSchema>;

export const ViewOrganizationSchema = object({
  id: string()
    .length(24, "Id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Id has invalid format"),
  created_utc: iso.datetime(),
  updated_utc: iso.datetime(),
  name: string().min(1, "Name is required"),
  plan_id: string().min(1, "Plan id is required"),
  plan_name: string().min(1, "Plan name is required"),
  plan_description: string().min(1, "Plan description is required"),
  card_last4: string().min(1, '"card last4" is required').nullable().optional(),
  subscribe_date: iso.datetime().nullable().optional(),
  billing_change_date: iso.datetime().nullable().optional(),
  billing_changed_by_user_id: string()
    .length(24, "Billing changed by user id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Billing changed by user id has invalid format")
    .nullable()
    .optional(),
  billing_status: BillingStatusSchema,
  billing_price: number(),
  max_events_per_month: int32(),
  bonus_events_per_month: int32(),
  bonus_expiration: iso.datetime().nullable().optional(),
  retention_days: int32(),
  is_suspended: boolean(),
  suspension_code: string()
    .min(1, "Suspension code is required")
    .nullable()
    .optional(),
  suspension_notes: string()
    .min(1, "Suspension notes is required")
    .nullable()
    .optional(),
  suspension_date: iso.datetime().nullable().optional(),
  has_premium_features: boolean(),
  max_users: int32(),
  max_projects: int32(),
  project_count: int(),
  stack_count: int(),
  event_count: int(),
  invites: array(lazy(() => InviteSchema)),
  usage_hours: array(lazy(() => UsageHourInfoSchema)),
  usage: array(lazy(() => UsageInfoSchema)),
  data: record(string(), unknown()).nullable().optional(),
  is_throttled: boolean(),
  is_over_monthly_limit: boolean(),
  is_over_request_limit: boolean(),
});
export type ViewOrganizationFormData = Infer<typeof ViewOrganizationSchema>;

export const ViewProjectSchema = object({
  id: string()
    .length(24, "Id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Id has invalid format"),
  created_utc: iso.datetime(),
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Organization id has invalid format"),
  organization_name: string().min(1, "Organization name is required"),
  name: string().min(1, "Name is required"),
  delete_bot_data_enabled: boolean(),
  data: record(string(), unknown()).nullable().optional(),
  promoted_tabs: array(string()),
  is_configured: boolean().nullable().optional(),
  stack_count: int(),
  event_count: int(),
  has_premium_features: boolean(),
  has_slack_integration: boolean(),
  usage_hours: array(lazy(() => UsageHourInfoSchema)),
  usage: array(lazy(() => UsageInfoSchema)),
});
export type ViewProjectFormData = Infer<typeof ViewProjectSchema>;

export const ViewTokenSchema = object({
  id: string()
    .length(24, "Id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Id has invalid format"),
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Organization id has invalid format"),
  project_id: string()
    .length(24, "Project id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Project id has invalid format"),
  user_id: string()
    .length(24, "User id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "User id has invalid format")
    .nullable()
    .optional(),
  default_project_id: string()
    .length(24, "Default project id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Default project id has invalid format")
    .nullable()
    .optional(),
  scopes: array(string()),
  expires_utc: iso.datetime().nullable().optional(),
  notes: string().min(1, "Notes is required").nullable().optional(),
  is_disabled: boolean(),
  is_suspended: boolean(),
  created_utc: iso.datetime(),
  updated_utc: iso.datetime(),
});
export type ViewTokenFormData = Infer<typeof ViewTokenSchema>;

export const ViewUserSchema = object({
  id: string()
    .length(24, "Id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Id has invalid format"),
  organization_ids: array(string()),
  full_name: string().min(1, "Full name is required"),
  email_address: email(),
  email_notifications_enabled: boolean(),
  is_email_address_verified: boolean(),
  is_active: boolean(),
  is_invite: boolean(),
  roles: array(string()),
});
export type ViewUserFormData = Infer<typeof ViewUserSchema>;

export const WebHookSchema = object({
  id: string()
    .length(24, "Id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Id has invalid format"),
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Organization id has invalid format"),
  project_id: string()
    .length(24, "Project id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Project id has invalid format"),
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
  is_enabled: boolean(),
  version: string(),
  created_utc: iso.datetime(),
});
export type WebHookFormData = Infer<typeof WebHookSchema>;

export const WorkInProgressResultSchema = object({
  workers: array(string()).optional(),
});
export type WorkInProgressResultFormData = Infer<
  typeof WorkInProgressResultSchema
>;
