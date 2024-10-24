/* eslint-disable */
/* tslint:disable */
/*
 * ---------------------------------------------------------------
 * ## THIS FILE WAS GENERATED VIA SWAGGER-TYPESCRIPT-API        ##
 * ##                                                           ##
 * ## AUTHOR: acacode                                           ##
 * ## SOURCE: https://github.com/acacode/swagger-typescript-api ##
 * ---------------------------------------------------------------
 */

// This file was generated by the templates in the /api-templates folder

import { IsDate, IsDefined, IsEmail, IsInt, IsMongoId, IsNumber, IsOptional, IsUrl, MaxLength, MinLength, ValidateNested } from 'class-validator';

export class BillingPlan {
    @IsMongoId({ message: 'id must be a valid ObjectId.' }) id!: string;
    @IsDefined({ message: 'name is required.' }) name!: string;
    @IsDefined({ message: 'description is required.' }) description!: string;
    /** @format double */
    @IsOptional() @IsNumber({}, { message: 'price must be a numeric value.' }) price?: number;
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'max_projects must be a whole number.' }) max_projects?: number;
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'max_users must be a whole number.' }) max_users?: number;
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'retention_days must be a whole number.' }) retention_days?: number;
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'max_events_per_month must be a whole number.' }) max_events_per_month?: number;
    @IsOptional() has_premium_features?: boolean;
    @IsOptional() is_hidden?: boolean;
}

/**
 *
 *
 * 0 = Trialing
 *
 * 1 = Active
 *
 * 2 = PastDue
 *
 * 3 = Canceled
 *
 * 4 = Unpaid
 * @format int32
 */
export enum BillingStatus {
    Trialing = 0,
    Active = 1,
    PastDue = 2,
    Canceled = 3,
    Unpaid = 4
}

export class ChangePasswordModel {
    @IsOptional() current_password?: string | null;
    @IsOptional() password?: string | null;
}

export class ChangePlanResult {
    @IsOptional() success?: boolean;
    @IsOptional() message?: string | null;
}

export class ClientConfiguration {
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'version must be a whole number.' }) version?: number;
    @IsOptional() @ValidateNested({ message: 'settings must be a valid nested object.' }) settings?: Record<string, string>;
}

export class CountResult {
    /** @format int64 */
    @IsOptional() @IsInt({ message: 'total must be a whole number.' }) total?: number;
    @IsOptional() @ValidateNested({ message: 'aggregations must be a valid nested object.' }) aggregations?: Record<string, IAggregate>;
    @IsOptional() @ValidateNested({ message: 'data must be a valid nested object.' }) data?: Record<string, unknown>;
}

export interface IAggregate {
    data?: Record<string, unknown>;
}

export class Invite {
    @IsDefined({ message: 'token is required.' }) token!: string;
    @IsDefined({ message: 'email_address is required.' }) email_address!: string;
    /** @format date-time */
    @IsDate({ message: 'date_added must be a valid date and time.' }) date_added!: string;
}

export class Invoice {
    @IsMongoId({ message: 'id must be a valid ObjectId.' }) id!: string;
    @IsMongoId({ message: 'organization_id must be a valid ObjectId.' }) organization_id!: string;
    @IsDefined({ message: 'organization_name is required.' }) organization_name!: string;
    /** @format date-time */
    @IsDate({ message: 'date must be a valid date and time.' }) date!: string;
    @IsDefined({ message: 'paid is required.' }) paid!: boolean;
    /** @format double */
    @IsNumber({}, { message: 'total must be a numeric value.' }) total!: number;
    @IsOptional() @ValidateNested({ message: 'items must be a valid nested object.' }) items?: InvoiceLineItem[];
}

export class InvoiceGridModel {
    @IsMongoId({ message: 'id must be a valid ObjectId.' }) id!: string;
    /** @format date-time */
    @IsDate({ message: 'date must be a valid date and time.' }) date!: string;
    @IsDefined({ message: 'paid is required.' }) paid!: boolean;
}

export class InvoiceLineItem {
    @IsDefined({ message: 'description is required.' }) description!: string;
    @IsOptional() date?: string | null;
    /** @format double */
    @IsNumber({}, { message: 'amount must be a numeric value.' }) amount!: number;
}

export class Login {
    /** @format email */
    @IsEmail({ require_tld: false }, { message: 'email must be a valid email address.' })
    @MinLength(1, { message: 'email must be at least 1 characters long.' })
    email!: string;
    @MinLength(6, { message: 'password must be at least 6 characters long.' })
    @MaxLength(100, { message: 'password must be at most 100 characters long.' })
    password!: string;
    @IsOptional()
    @MinLength(40, { message: 'invite_token must be at least 40 characters long.' })
    @MaxLength(40, { message: 'invite_token must be at most 40 characters long.' })
    invite_token?: string | null;
}

export class NewOrganization {
    @IsOptional() name?: string;
}

export class NewProject {
    @IsOptional() @IsMongoId({ message: 'organization_id must be a valid ObjectId.' }) organization_id?: string;
    @IsOptional() name?: string;
    @IsOptional() delete_bot_data_enabled?: boolean;
}

export class NewToken {
    @IsOptional() @IsMongoId({ message: 'organization_id must be a valid ObjectId.' }) organization_id?: string;
    @IsOptional() @IsMongoId({ message: 'project_id must be a valid ObjectId.' }) project_id?: string;
    @IsOptional() @IsMongoId({ message: 'default_project_id must be a valid ObjectId.' }) default_project_id?: string | null;
    @IsOptional() scopes?: string[];
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'expires_utc must be a valid date and time.' }) expires_utc?: string | null;
    @IsOptional() notes?: string | null;
}

export class NewWebHook {
    @IsOptional() @IsMongoId({ message: 'organization_id must be a valid ObjectId.' }) organization_id?: string;
    @IsOptional() @IsMongoId({ message: 'project_id must be a valid ObjectId.' }) project_id?: string;
    @IsOptional() @IsUrl({}, { message: 'url must be a valid URL.' }) url?: string;
    @IsOptional() event_types?: string[];
    /** The schema version that should be used. */
    @IsOptional() version?: string | null;
}

export class NotificationSettings {
    @IsOptional() send_daily_summary?: boolean;
    @IsOptional() report_new_errors?: boolean;
    @IsOptional() report_critical_errors?: boolean;
    @IsOptional() report_event_regressions?: boolean;
    @IsOptional() report_new_events?: boolean;
    @IsOptional() report_critical_events?: boolean;
}

export class OAuthAccount {
    @IsDefined({ message: 'provider is required.' }) provider!: string;
    @IsMongoId({ message: 'provider_user_id must be a valid ObjectId.' }) provider_user_id!: string;
    @IsDefined({ message: 'username is required.' }) username!: string;
    @IsOptional() @ValidateNested({ message: 'extra_data must be a valid nested object.' }) extra_data?: Record<string, string>;
}

export class PersistentEvent {
    @IsOptional() @IsMongoId({ message: 'id must be a valid ObjectId.' }) id?: string;
    @IsOptional() @IsMongoId({ message: 'organization_id must be a valid ObjectId.' }) organization_id?: string;
    @IsOptional() @IsMongoId({ message: 'project_id must be a valid ObjectId.' }) project_id?: string;
    @IsOptional() @IsMongoId({ message: 'stack_id must be a valid ObjectId.' }) stack_id?: string;
    @IsOptional() is_first_occurrence?: boolean;
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'created_utc must be a valid date and time.' }) created_utc?: string;
    @IsOptional() @ValidateNested({ message: 'idx must be a valid nested object.' }) idx?: Record<string, unknown>;
    @IsOptional() type?: string | null;
    @IsOptional() source?: string | null;
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'date must be a valid date and time.' }) date?: string;
    @IsOptional() tags?: string[] | null;
    @IsOptional() message?: string | null;
    @IsOptional() geo?: string | null;
    /** @format double */
    @IsOptional() @IsNumber({}, { message: 'value must be a numeric value.' }) value?: number | null;
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'count must be a whole number.' }) count?: number | null;
    @IsOptional() @ValidateNested({ message: 'data must be a valid nested object.' }) data?: Record<string, unknown>;
    @IsOptional() @IsMongoId({ message: 'reference_id must be a valid ObjectId.' }) reference_id?: string | null;
}

export class ResetPasswordModel {
    @IsOptional() password_reset_token?: string | null;
    @IsOptional() password?: string | null;
}

export class Signup {
    @MinLength(1, { message: 'name must be at least 1 characters long.' }) name!: string;
    /** @format email */
    @IsEmail({ require_tld: false }, { message: 'email must be a valid email address.' })
    @MinLength(1, { message: 'email must be at least 1 characters long.' })
    email!: string;
    @MinLength(6, { message: 'password must be at least 6 characters long.' })
    @MaxLength(100, { message: 'password must be at most 100 characters long.' })
    password!: string;
    @IsOptional()
    @MinLength(40, { message: 'invite_token must be at least 40 characters long.' })
    @MaxLength(40, { message: 'invite_token must be at most 40 characters long.' })
    invite_token?: string | null;
}

export class Stack {
    @IsOptional() @IsMongoId({ message: 'id must be a valid ObjectId.' }) id?: string;
    @IsOptional() @IsMongoId({ message: 'organization_id must be a valid ObjectId.' }) organization_id?: string;
    @IsOptional() @IsMongoId({ message: 'project_id must be a valid ObjectId.' }) project_id?: string;
    @IsOptional() type?: string;
    /**
     *
     * open
     * fixed
     * regressed
     * snoozed
     * ignored
     * discarded
     */
    @IsOptional() status?: StackStatus;
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'snooze_until_utc must be a valid date and time.' }) snooze_until_utc?: string | null;
    @IsOptional() signature_hash?: string;
    @IsOptional() @ValidateNested({ message: 'signature_info must be a valid nested object.' }) signature_info?: Record<string, string>;
    @IsOptional() fixed_in_version?: string | null;
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'date_fixed must be a valid date and time.' }) date_fixed?: string | null;
    @IsOptional() title?: string;
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'total_occurrences must be a whole number.' }) total_occurrences?: number;
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'first_occurrence must be a valid date and time.' }) first_occurrence?: string;
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'last_occurrence must be a valid date and time.' }) last_occurrence?: string;
    @IsOptional() description?: string | null;
    @IsOptional() occurrences_are_critical?: boolean;
    @IsOptional() references?: string[];
    @IsOptional() tags?: string[];
    @IsOptional() duplicate_signature?: string;
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'created_utc must be a valid date and time.' }) created_utc?: string;
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'updated_utc must be a valid date and time.' }) updated_utc?: string;
    @IsOptional() is_deleted?: boolean;
    @IsOptional() allow_notifications?: boolean;
}

/**
 *
 *
 * open
 *
 * fixed
 *
 * regressed
 *
 * snoozed
 *
 * ignored
 *
 * discarded
 */
export enum StackStatus {
    Open = 'open',
    Fixed = 'fixed',
    Regressed = 'regressed',
    Snoozed = 'snoozed',
    Ignored = 'ignored',
    Discarded = 'discarded'
}

export class StringStringValuesKeyValuePair {
    @IsOptional() key?: string | null;
    @IsOptional() value?: string[];
}

export class StringValueFromBody {
    @IsOptional() value?: string | null;
}

export class TokenResult {
    @MinLength(1, { message: 'token must be at least 1 characters long.' }) token!: string;
}

export class UpdateEmailAddressResult {
    @IsOptional() is_verified?: boolean;
}

export class UsageHourInfo {
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'date must be a valid date and time.' }) date?: string;
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'total must be a whole number.' }) total?: number;
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'blocked must be a whole number.' }) blocked?: number;
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'discarded must be a whole number.' }) discarded?: number;
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'too_big must be a whole number.' }) too_big?: number;
}

export class UsageInfo {
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'date must be a valid date and time.' }) date?: string;
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'limit must be a whole number.' }) limit?: number;
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'total must be a whole number.' }) total?: number;
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'blocked must be a whole number.' }) blocked?: number;
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'discarded must be a whole number.' }) discarded?: number;
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'too_big must be a whole number.' }) too_big?: number;
}

export class User {
    @IsOptional() @IsMongoId({ message: 'id must be a valid ObjectId.' }) id?: string;
    @IsOptional() organization_ids?: string[];
    @IsOptional() password?: string | null;
    @IsOptional() salt?: string | null;
    @IsOptional() password_reset_token?: string | null;
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'password_reset_token_expiration must be a valid date and time.' }) password_reset_token_expiration?: string;
    @IsOptional() @ValidateNested({ message: 'o_auth_accounts must be a valid nested object.' }) o_auth_accounts?: OAuthAccount[];
    @IsOptional() full_name?: string;
    @IsOptional() email_address?: string;
    @IsOptional() email_notifications_enabled?: boolean;
    @IsOptional() is_email_address_verified?: boolean;
    @IsOptional() verify_email_address_token?: string | null;
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'verify_email_address_token_expiration must be a valid date and time.' }) verify_email_address_token_expiration?: string;
    @IsOptional() is_active?: boolean;
    @IsOptional() roles?: string[];
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'created_utc must be a valid date and time.' }) created_utc?: string;
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'updated_utc must be a valid date and time.' }) updated_utc?: string;
}

export class UserDescription {
    @IsOptional() email_address?: string | null;
    @IsOptional() description?: string | null;
    @IsOptional() @ValidateNested({ message: 'data must be a valid nested object.' }) data?: Record<string, unknown>;
}

export class ViewOrganization {
    @IsOptional() @IsMongoId({ message: 'id must be a valid ObjectId.' }) id?: string;
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'created_utc must be a valid date and time.' }) created_utc?: string;
    @IsOptional() name?: string;
    @IsOptional() @IsMongoId({ message: 'plan_id must be a valid ObjectId.' }) plan_id?: string;
    @IsOptional() plan_name?: string;
    @IsOptional() plan_description?: string;
    @IsOptional() 'card_last4'?: string | null;
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'subscribe_date must be a valid date and time.' }) subscribe_date?: string | null;
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'billing_change_date must be a valid date and time.' }) billing_change_date?: string | null;
    @IsOptional() @IsMongoId({ message: 'billing_changed_by_user_id must be a valid ObjectId.' }) billing_changed_by_user_id?: string | null;
    /**
     *
     * 0 = Trialing
     * 1 = Active
     * 2 = PastDue
     * 3 = Canceled
     * 4 = Unpaid
     */
    @IsOptional() billing_status?: BillingStatus;
    /** @format double */
    @IsOptional() @IsNumber({}, { message: 'billing_price must be a numeric value.' }) billing_price?: number;
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'max_events_per_month must be a whole number.' }) max_events_per_month?: number;
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'bonus_events_per_month must be a whole number.' }) bonus_events_per_month?: number;
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'bonus_expiration must be a valid date and time.' }) bonus_expiration?: string | null;
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'retention_days must be a whole number.' }) retention_days?: number;
    @IsOptional() is_suspended?: boolean;
    @IsOptional() suspension_code?: string | null;
    @IsOptional() suspension_notes?: string | null;
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'suspension_date must be a valid date and time.' }) suspension_date?: string | null;
    @IsOptional() has_premium_features?: boolean;
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'max_users must be a whole number.' }) max_users?: number;
    /** @format int32 */
    @IsOptional() @IsInt({ message: 'max_projects must be a whole number.' }) max_projects?: number;
    /** @format int64 */
    @IsOptional() @IsInt({ message: 'project_count must be a whole number.' }) project_count?: number;
    /** @format int64 */
    @IsOptional() @IsInt({ message: 'stack_count must be a whole number.' }) stack_count?: number;
    /** @format int64 */
    @IsOptional() @IsInt({ message: 'event_count must be a whole number.' }) event_count?: number;
    @IsOptional() @ValidateNested({ message: 'invites must be a valid nested object.' }) invites?: Invite[];
    @IsOptional() @ValidateNested({ message: 'usage_hours must be a valid nested object.' }) usage_hours?: UsageHourInfo[];
    @IsOptional() @ValidateNested({ message: 'usage must be a valid nested object.' }) usage?: UsageInfo[];
    @IsOptional() @ValidateNested({ message: 'data must be a valid nested object.' }) data?: Record<string, unknown>;
    @IsOptional() is_throttled?: boolean;
    @IsOptional() is_over_monthly_limit?: boolean;
    @IsOptional() is_over_request_limit?: boolean;
}

export class ViewProject {
    @IsOptional() @IsMongoId({ message: 'id must be a valid ObjectId.' }) id?: string;
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'created_utc must be a valid date and time.' }) created_utc?: string;
    @IsOptional() @IsMongoId({ message: 'organization_id must be a valid ObjectId.' }) organization_id?: string;
    @IsOptional() organization_name?: string;
    @IsOptional() name?: string;
    @IsOptional() delete_bot_data_enabled?: boolean;
    @IsOptional() @ValidateNested({ message: 'data must be a valid nested object.' }) data?: Record<string, unknown>;
    @IsOptional() promoted_tabs?: string[];
    @IsOptional() is_configured?: boolean | null;
    /** @format int64 */
    @IsOptional() @IsInt({ message: 'stack_count must be a whole number.' }) stack_count?: number;
    /** @format int64 */
    @IsOptional() @IsInt({ message: 'event_count must be a whole number.' }) event_count?: number;
    @IsOptional() has_premium_features?: boolean;
    @IsOptional() has_slack_integration?: boolean;
    @IsOptional() @ValidateNested({ message: 'usage_hours must be a valid nested object.' }) usage_hours?: UsageHourInfo[];
    @IsOptional() @ValidateNested({ message: 'usage must be a valid nested object.' }) usage?: UsageInfo[];
}

export class ViewToken {
    @IsOptional() @IsMongoId({ message: 'id must be a valid ObjectId.' }) id?: string;
    @IsOptional() @IsMongoId({ message: 'organization_id must be a valid ObjectId.' }) organization_id?: string;
    @IsOptional() @IsMongoId({ message: 'project_id must be a valid ObjectId.' }) project_id?: string;
    @IsOptional() @IsMongoId({ message: 'user_id must be a valid ObjectId.' }) user_id?: string | null;
    @IsOptional() @IsMongoId({ message: 'default_project_id must be a valid ObjectId.' }) default_project_id?: string | null;
    @IsOptional() scopes?: string[];
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'expires_utc must be a valid date and time.' }) expires_utc?: string | null;
    @IsOptional() notes?: string | null;
    @IsOptional() is_disabled?: boolean;
    @IsOptional() is_suspended?: boolean;
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'created_utc must be a valid date and time.' }) created_utc?: string;
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'updated_utc must be a valid date and time.' }) updated_utc?: string;
}

export class ViewUser {
    @IsOptional() @IsMongoId({ message: 'id must be a valid ObjectId.' }) id?: string;
    @IsOptional() organization_ids?: string[];
    @IsOptional() full_name?: string;
    @IsOptional() email_address?: string;
    @IsOptional() email_notifications_enabled?: boolean;
    @IsOptional() is_email_address_verified?: boolean;
    @IsOptional() is_active?: boolean;
    @IsOptional() is_invite?: boolean;
    @IsOptional() roles?: string[];
}

export class WebHook {
    @IsOptional() @IsMongoId({ message: 'id must be a valid ObjectId.' }) id?: string;
    @IsOptional() @IsMongoId({ message: 'organization_id must be a valid ObjectId.' }) organization_id?: string;
    @IsOptional() @IsMongoId({ message: 'project_id must be a valid ObjectId.' }) project_id?: string;
    @IsOptional() @IsUrl({}, { message: 'url must be a valid URL.' }) url?: string;
    @IsOptional() event_types?: string[];
    @IsOptional() is_enabled?: boolean;
    @IsOptional() version?: string;
    /** @format date-time */
    @IsOptional() @IsDate({ message: 'created_utc must be a valid date and time.' }) created_utc?: string;
}
