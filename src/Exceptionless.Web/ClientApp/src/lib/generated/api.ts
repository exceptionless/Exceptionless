export enum StackStatus {
  Open = "open",
  Fixed = "fixed",
  Regressed = "regressed",
  Snoozed = "snoozed",
  Ignored = "ignored",
  Discarded = "discarded",
}

export enum BillingStatus {
  Trialing = 0,
  Active = 1,
  PastDue = 2,
  Canceled = 3,
  Unpaid = 4,
}

export interface BillingPlan {
  id: string;
  name: string;
  description: string;
  /** @format double */
  price: number;
  /** @format int32 */
  max_projects: number;
  /** @format int32 */
  max_users: number;
  /** @format int32 */
  retention_days: number;
  /** @format int32 */
  max_events_per_month: number;
  has_premium_features: boolean;
  is_hidden: boolean;
}

export interface ChangePasswordModel {
  current_password: string;
  password: string;
}

export interface ChangePlanResult {
  success: boolean;
  message?: null | string;
}

export interface ClientConfiguration {
  /** @format int32 */
  version: number;
  settings: Record<string, string>;
}

export interface CountResult {
  /** @format int64 */
  total: number;
  aggregations?: null | Record<string, IAggregate>;
  data?: null | object;
}

export interface ExternalAuthInfo {
  clientId: string;
  code: string;
  /** @format uri */
  redirectUri: string;
  inviteToken?: null | string;
}

/** Base interface for aggregation results. Concrete types include ValueAggregate, BucketAggregate, StatsAggregate, etc. See client-side type definitions for full type information. */
export interface IAggregate {
  /** Additional data associated with the aggregate. */
  data: Record<string, unknown>;
}

export interface Invite {
  token: string;
  /** @format email */
  email_address: string;
  /** @format date-time */
  date_added: string;
}

export interface Invoice {
  id: string;
  /** @pattern ^[a-fA-F0-9]{24}$ */
  organization_id: string;
  organization_name: string;
  /** @format date-time */
  date: string;
  paid: boolean;
  /** @format double */
  total: number;
  items: InvoiceLineItem[];
}

export interface InvoiceGridModel {
  id: string;
  /** @format date-time */
  date: string;
  paid: boolean;
}

export interface InvoiceLineItem {
  description: string;
  date?: null | string;
  /** @format double */
  amount: number;
}

export interface Login {
  /** The email address or domain username */
  email: string;
  password: string;
  invite_token?: null | string;
}

export interface NewOrganization {
  name: string;
}

export interface NewProject {
  /** @pattern ^[a-fA-F0-9]{24}$ */
  organization_id: string;
  name: string;
  delete_bot_data_enabled: boolean;
}

export interface NewToken {
  /** @pattern ^[a-fA-F0-9]{24}$ */
  organization_id?: null | string;
  /** @pattern ^[a-fA-F0-9]{24}$ */
  project_id?: null | string;
  /** @pattern ^[a-fA-F0-9]{24}$ */
  default_project_id?: null | string;
  scopes: string[];
  /** @format date-time */
  expires_utc?: null | string;
  notes?: null | string;
}

export interface NewWebHook {
  /** @pattern ^[a-fA-F0-9]{24}$ */
  organization_id: string;
  /** @pattern ^[a-fA-F0-9]{24}$ */
  project_id: string;
  /** @format uri */
  url: string;
  event_types: string[];
  /**
   * The schema version that should be used.
   * @pattern ^\d+(\.\d+){1,3}$
   */
  version?: null | string;
}

export interface NotificationSettings {
  send_daily_summary: boolean;
  report_new_errors: boolean;
  report_critical_errors: boolean;
  report_event_regressions: boolean;
  report_new_events: boolean;
  report_critical_events: boolean;
}

export interface OAuthAccount {
  provider: string;
  provider_user_id: string;
  username: string;
  extra_data?: null | object;
}

export interface PersistentEvent {
  /**
   * Unique id that identifies an event.
   * @pattern ^[a-fA-F0-9]{24}$
   */
  id: string;
  /**
   * The organization that the event belongs to.
   * @pattern ^[a-fA-F0-9]{24}$
   */
  organization_id: string;
  /**
   * The project that the event belongs to.
   * @pattern ^[a-fA-F0-9]{24}$
   */
  project_id: string;
  /**
   * The stack that the event belongs to.
   * @pattern ^[a-fA-F0-9]{24}$
   */
  stack_id: string;
  /** Whether the event resulted in the creation of a new stack. */
  is_first_occurrence: boolean;
  /**
   * The date that the event was created in the system.
   * @format date-time
   */
  created_utc: string;
  /** Used to store primitive data type custom data values for searching the event. */
  idx: Record<string, unknown>;
  /** The event type (ie. error, log message, feature usage). Check KnownTypes for standard event types. */
  type?: null | string;
  /** The event source (ie. machine name, log name, feature name). */
  source?: null | string;
  /**
   * The date that the event occurred on.
   * @format date-time
   */
  date: string;
  /** A list of tags used to categorize this event. */
  tags?: string[] | null;
  /** The event message. */
  message?: null | string;
  /** The geo coordinates where the event happened. */
  geo?: null | string;
  /**
   * The value of the event if any.
   * @format double
   */
  value?: null | number;
  /**
   * The number of duplicated events.
   * @format int32
   */
  count?: null | number;
  /** Optional data entries that contain additional information about this event. */
  data?: null | object;
  /** An optional identifier to be used for referencing this event instance at a later time. */
  reference_id?: null | string;
}

export interface ResetPasswordModel {
  password_reset_token: string;
  password: string;
}

export interface Signup {
  name: string;
  /** The email address or domain username */
  email: string;
  password: string;
  invite_token?: null | string;
}

export interface Stack {
  /**
   * Unique id that identifies a stack.
   * @pattern ^[a-fA-F0-9]{24}$
   */
  id: string;
  /**
   * The organization that the stack belongs to.
   * @pattern ^[a-fA-F0-9]{24}$
   */
  organization_id: string;
  /**
   * The project that the stack belongs to.
   * @pattern ^[a-fA-F0-9]{24}$
   */
  project_id: string;
  /** The stack type (ie. error, log message, feature usage). Check KnownTypes for standard stack types. */
  type: string;
  /** The stack status (ie. open, fixed, regressed, */
  status: StackStatus;
  /**
   * The date that the stack should be snoozed until.
   * @format date-time
   */
  snooze_until_utc?: null | string;
  /** The signature used for stacking future occurrences. */
  signature_hash: string;
  /** The collection of information that went into creating the signature hash for the stack. */
  signature_info: Record<string, string>;
  /** The version the stack was fixed in. */
  fixed_in_version?: null | string;
  /**
   * The date the stack was fixed.
   * @format date-time
   */
  date_fixed?: null | string;
  /** The stack title. */
  title: string;
  /**
   * The total number of occurrences in the stack.
   * @format int32
   */
  total_occurrences: number;
  /**
   * The date of the 1st occurrence of this stack in UTC time.
   * @format date-time
   */
  first_occurrence: string;
  /**
   * The date of the last occurrence of this stack in UTC time.
   * @format date-time
   */
  last_occurrence: string;
  /** The stack description. */
  description?: null | string;
  /** If true, all future occurrences will be marked as critical. */
  occurrences_are_critical: boolean;
  /** A list of references. */
  references: string[];
  /** A list of tags used to categorize this stack. */
  tags: string[];
  /** The signature used for finding duplicate stacks. (ProjectId, SignatureHash) */
  duplicate_signature: string;
  /** @format date-time */
  created_utc: string;
  /** @format date-time */
  updated_utc: string;
  is_deleted: boolean;
  allow_notifications: boolean;
}

export interface StringStringValuesKeyValuePair {
  key?: null | string;
  value: string[];
}

export interface StringValueFromBody {
  value?: null | string;
}

export interface TokenResult {
  token: string;
}

export interface UpdateEmailAddressResult {
  is_verified: boolean;
}

/** A class the tracks changes (i.e. the Delta) for a particular TEntityType. */
export interface UpdateEvent {
  /** @format email */
  email_address?: null | string;
  description?: null | string;
}

/** A class the tracks changes (i.e. the Delta) for a particular TEntityType. */
export interface UpdateProject {
  name: string;
  delete_bot_data_enabled: boolean;
}

/** A class the tracks changes (i.e. the Delta) for a particular TEntityType. */
export interface UpdateToken {
  is_disabled: boolean;
  notes?: null | string;
}

/** A class the tracks changes (i.e. the Delta) for a particular TEntityType. */
export interface UpdateUser {
  full_name: string;
  email_notifications_enabled: boolean;
}

export interface UsageHourInfo {
  /** @format date-time */
  date: string;
  /** @format int32 */
  total: number;
  /** @format int32 */
  blocked: number;
  /** @format int32 */
  discarded: number;
  /** @format int32 */
  too_big: number;
}

export interface UsageInfo {
  /** @format date-time */
  date: string;
  /** @format int32 */
  limit: number;
  /** @format int32 */
  total: number;
  /** @format int32 */
  blocked: number;
  /** @format int32 */
  discarded: number;
  /** @format int32 */
  too_big: number;
}

export interface User {
  /**
   * Unique id that identifies an user.
   * @pattern ^[a-fA-F0-9]{24}$
   */
  id: string;
  /** The organizations that the user has access to. */
  organization_ids: string[];
  password?: null | string;
  salt?: null | string;
  password_reset_token?: null | string;
  /** @format date-time */
  password_reset_token_expiration: string;
  o_auth_accounts: OAuthAccount[];
  /** Gets or sets the users Full Name. */
  full_name: string;
  /** @format email */
  email_address: string;
  email_notifications_enabled: boolean;
  is_email_address_verified: boolean;
  verify_email_address_token?: null | string;
  /** @format date-time */
  verify_email_address_token_expiration: string;
  /** Gets or sets the users active state. */
  is_active: boolean;
  roles: string[];
  /** @format date-time */
  created_utc: string;
  /** @format date-time */
  updated_utc: string;
}

export interface UserDescription {
  /** @format email */
  email_address?: null | string;
  description?: null | string;
  /** Extended data entries for this user description. */
  data?: null | object;
}

export interface ViewCurrentUser {
  hash?: null | string;
  has_local_account: boolean;
  o_auth_accounts: OAuthAccount[];
  /** @pattern ^[a-fA-F0-9]{24}$ */
  id: string;
  organization_ids: string[];
  full_name: string;
  /** @format email */
  email_address: string;
  email_notifications_enabled: boolean;
  is_email_address_verified: boolean;
  is_active: boolean;
  is_invite: boolean;
  roles: string[];
}

export interface ViewOrganization {
  /** @pattern ^[a-fA-F0-9]{24}$ */
  id: string;
  /** @format date-time */
  created_utc: string;
  /** @format date-time */
  updated_utc: string;
  name: string;
  plan_id: string;
  plan_name: string;
  plan_description: string;
  card_last4?: null | string;
  /** @format date-time */
  subscribe_date?: null | string;
  /** @format date-time */
  billing_change_date?: null | string;
  /** @pattern ^[a-fA-F0-9]{24}$ */
  billing_changed_by_user_id?: null | string;
  billing_status: BillingStatus;
  /** @format double */
  billing_price: number;
  /** @format int32 */
  max_events_per_month: number;
  /** @format int32 */
  bonus_events_per_month: number;
  /** @format date-time */
  bonus_expiration?: null | string;
  /** @format int32 */
  retention_days: number;
  is_suspended: boolean;
  suspension_code?: null | string;
  suspension_notes?: null | string;
  /** @format date-time */
  suspension_date?: null | string;
  has_premium_features: boolean;
  /** @format int32 */
  max_users: number;
  /** @format int32 */
  max_projects: number;
  /** @format int64 */
  project_count: number;
  /** @format int64 */
  stack_count: number;
  /** @format int64 */
  event_count: number;
  invites: Invite[];
  usage_hours: UsageHourInfo[];
  usage: UsageInfo[];
  data?: null | object;
  is_throttled: boolean;
  is_over_monthly_limit: boolean;
  is_over_request_limit: boolean;
}

export interface ViewProject {
  /** @pattern ^[a-fA-F0-9]{24}$ */
  id: string;
  /** @format date-time */
  created_utc: string;
  /** @pattern ^[a-fA-F0-9]{24}$ */
  organization_id: string;
  organization_name: string;
  name: string;
  delete_bot_data_enabled: boolean;
  data?: null | object;
  promoted_tabs: string[];
  is_configured?: null | boolean;
  /** @format int64 */
  stack_count: number;
  /** @format int64 */
  event_count: number;
  has_premium_features: boolean;
  has_slack_integration: boolean;
  usage_hours: UsageHourInfo[];
  usage: UsageInfo[];
}

export interface ViewToken {
  /** @pattern ^[a-fA-F0-9]{24}$ */
  id: string;
  /** @pattern ^[a-fA-F0-9]{24}$ */
  organization_id: string;
  /** @pattern ^[a-fA-F0-9]{24}$ */
  project_id: string;
  /** @pattern ^[a-fA-F0-9]{24}$ */
  user_id?: null | string;
  /** @pattern ^[a-fA-F0-9]{24}$ */
  default_project_id?: null | string;
  scopes: string[];
  /** @format date-time */
  expires_utc?: null | string;
  notes?: null | string;
  is_disabled: boolean;
  is_suspended: boolean;
  /** @format date-time */
  created_utc: string;
  /** @format date-time */
  updated_utc: string;
}

export interface ViewUser {
  /** @pattern ^[a-fA-F0-9]{24}$ */
  id: string;
  organization_ids: string[];
  full_name: string;
  /** @format email */
  email_address: string;
  email_notifications_enabled: boolean;
  is_email_address_verified: boolean;
  is_active: boolean;
  is_invite: boolean;
  roles: string[];
}

export interface WebHook {
  /** @pattern ^[a-fA-F0-9]{24}$ */
  id: string;
  /** @pattern ^[a-fA-F0-9]{24}$ */
  organization_id: string;
  /** @pattern ^[a-fA-F0-9]{24}$ */
  project_id: string;
  /** @format uri */
  url: string;
  event_types: string[];
  is_enabled: boolean;
  /** The schema version that should be used. */
  version: string;
  /** @format date-time */
  created_utc: string;
}

export interface WorkInProgressResult {
  workers: string[];
}
