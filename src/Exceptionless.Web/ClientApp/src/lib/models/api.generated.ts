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

import {
	IsDate,
	IsDefined,
	IsEmail,
	IsInt,
	IsMongoId,
	IsNumber,
	IsOptional,
	IsUrl,
	MaxLength,
	MinLength,
	ValidateNested
} from 'class-validator';

export class BillingPlan {
	@IsOptional() @IsMongoId() id?: string;
	@IsOptional() name?: string;
	@IsOptional() description?: string;
	/** @format double */
	@IsOptional() @IsNumber() price?: number;
	/** @format int32 */
	@IsOptional() @IsInt() max_projects?: number;
	/** @format int32 */
	@IsOptional() @IsInt() max_users?: number;
	/** @format int32 */
	@IsOptional() @IsInt() retention_days?: number;
	/** @format int32 */
	@IsOptional() @IsInt() max_events_per_month?: number;
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
	@IsOptional() @IsInt() version?: number;
	@IsOptional() @ValidateNested() settings?: Record<string, string>;
}

export class CountResult {
	/** @format int64 */
	@IsOptional() @IsInt() total?: number;
	@IsOptional() @ValidateNested() aggregations?: Record<string, IAggregate>;
	@IsOptional() @ValidateNested() data?: Record<string, unknown>;
}

export interface IAggregate {
	data?: Record<string, any>;
}

export class Invite {
	@IsOptional() token?: string;
	@IsOptional() email_address?: string;
	/** @format date-time */
	@IsOptional() @IsDate() date_added?: string;
}

export class Invoice {
	@IsOptional() @IsMongoId() id?: string;
	@IsOptional() @IsMongoId() organization_id?: string;
	@IsOptional() organization_name?: string;
	/** @format date-time */
	@IsOptional() @IsDate() date?: string;
	@IsOptional() paid?: boolean;
	/** @format double */
	@IsOptional() @IsNumber() total?: number;
	@IsOptional() @ValidateNested() items?: InvoiceLineItem[];
}

export class InvoiceGridModel {
	@IsOptional() @IsMongoId() id?: string;
	/** @format date-time */
	@IsOptional() @IsDate() date?: string;
	@IsOptional() paid?: boolean;
}

export class InvoiceLineItem {
	@IsOptional() description?: string;
	@IsOptional() date?: string | null;
	/** @format double */
	@IsOptional() @IsNumber() amount?: number;
}

export class LoginModel {
	constructor(email: string, password: string) {
		this.email = email;
		this.password = password;
	}

	/** @format email */
	@IsDefined() @IsEmail({ require_tld: false }) @MinLength(1) email: string;
	@IsDefined() @MinLength(6) @MaxLength(100) password: string;
	@IsOptional() invite_token?: string | null;
}

export class NewOrganization {
	@IsOptional() name?: string;
}

export class NewProject {
	@IsOptional() @IsMongoId() organization_id?: string;
	@IsOptional() name?: string;
	@IsOptional() delete_bot_data_enabled?: boolean;
}

export class NewToken {
	@IsOptional() @IsMongoId() organization_id?: string;
	@IsOptional() @IsMongoId() project_id?: string;
	@IsOptional() @IsMongoId() default_project_id?: string | null;
	@IsOptional() scopes?: string[];
	/** @format date-time */
	@IsOptional() @IsDate() expires_utc?: string | null;
	@IsOptional() notes?: string | null;
}

export class NewWebHook {
	@IsOptional() @IsMongoId() organization_id?: string;
	@IsOptional() @IsMongoId() project_id?: string;
	@IsOptional() @IsUrl() url?: string;
	@IsOptional() event_types?: string[];
	/** The schema version that should be used. */
	@IsOptional() version?: string;
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
	@IsOptional() provider?: string;
	@IsOptional() @IsMongoId() provider_user_id?: string;
	@IsOptional() username?: string;
	@IsOptional() @ValidateNested() extra_data?: Record<string, string>;
}

export class PersistentEvent {
	@IsOptional() @IsMongoId() id?: string;
	@IsOptional() @IsMongoId() organization_id?: string;
	@IsOptional() @IsMongoId() project_id?: string;
	@IsOptional() @IsMongoId() stack_id?: string;
	@IsOptional() is_first_occurrence?: boolean;
	/** @format date-time */
	@IsOptional() @IsDate() created_utc?: string;
	@IsOptional() @ValidateNested() idx?: Record<string, unknown>;
	@IsOptional() type?: string | null;
	@IsOptional() source?: string | null;
	/** @format date-time */
	@IsOptional() @IsDate() date?: string;
	@IsOptional() tags?: string[] | null;
	@IsOptional() message?: string | null;
	@IsOptional() geo?: string | null;
	/** @format double */
	@IsOptional() @IsNumber() value?: number | null;
	/** @format int32 */
	@IsOptional() @IsInt() count?: number | null;
	@IsOptional() @ValidateNested() data?: Record<string, unknown>;
	@IsOptional() @IsMongoId() reference_id?: string | null;
}

export class ResetPasswordModel {
	@IsOptional() password_reset_token?: string | null;
	@IsOptional() password?: string | null;
}

export class SignupModel {
	constructor(email: string, password: string) {
		this.email = email;
		this.password = password;
	}

	@IsOptional() name?: string;
	/** @format email */
	@IsDefined() @IsEmail({ require_tld: false }) @MinLength(1) email: string;
	@IsDefined() @MinLength(6) @MaxLength(100) password: string;
	@IsOptional() invite_token?: string | null;
}

export class Stack {
	@IsOptional() @IsMongoId() id?: string;
	@IsOptional() @IsMongoId() organization_id?: string;
	@IsOptional() @IsMongoId() project_id?: string;
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
	@IsOptional() @IsDate() snooze_until_utc?: string | null;
	@IsOptional() signature_hash?: string;
	@IsOptional() @ValidateNested() signature_info?: Record<string, string>;
	@IsOptional() fixed_in_version?: string | null;
	/** @format date-time */
	@IsOptional() @IsDate() date_fixed?: string | null;
	@IsOptional() title?: string;
	/** @format int32 */
	@IsOptional() @IsInt() total_occurrences?: number;
	/** @format date-time */
	@IsOptional() @IsDate() first_occurrence?: string;
	/** @format date-time */
	@IsOptional() @IsDate() last_occurrence?: string;
	@IsOptional() description?: string | null;
	@IsOptional() occurrences_are_critical?: boolean;
	@IsOptional() references?: string[];
	@IsOptional() tags?: string[];
	@IsOptional() duplicate_signature?: string;
	/** @format date-time */
	@IsOptional() @IsDate() created_utc?: string;
	/** @format date-time */
	@IsOptional() @IsDate() updated_utc?: string;
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
	@IsOptional() key?: string;
	@IsOptional() value?: string[];
}

export class StringValueFromBody {
	@IsOptional() value?: string;
}

export class TokenResult {
	@IsOptional() token?: string;
}

export class UpdateEmailAddressResult {
	@IsOptional() is_verified?: boolean;
}

export class UsageHourInfo {
	/** @format date-time */
	@IsOptional() @IsDate() date?: string;
	/** @format int32 */
	@IsOptional() @IsInt() total?: number;
	/** @format int32 */
	@IsOptional() @IsInt() blocked?: number;
	/** @format int32 */
	@IsOptional() @IsInt() discarded?: number;
	/** @format int32 */
	@IsOptional() @IsInt() too_big?: number;
}

export class UsageInfo {
	/** @format date-time */
	@IsOptional() @IsDate() date?: string;
	/** @format int32 */
	@IsOptional() @IsInt() limit?: number;
	/** @format int32 */
	@IsOptional() @IsInt() total?: number;
	/** @format int32 */
	@IsOptional() @IsInt() blocked?: number;
	/** @format int32 */
	@IsOptional() @IsInt() discarded?: number;
	/** @format int32 */
	@IsOptional() @IsInt() too_big?: number;
}

export class User {
	constructor(full_name: string, email_address: string) {
		this.full_name = full_name;
		this.email_address = email_address;
	}

	@IsOptional() @IsMongoId() id?: string;
	@IsOptional() organization_ids?: string[];
	@IsOptional() password?: string | null;
	@IsOptional() salt?: string | null;
	@IsOptional() password_reset_token?: string | null;
	/** @format date-time */
	@IsOptional() @IsDate() password_reset_token_expiration?: string;
	@IsOptional() @ValidateNested() o_auth_accounts?: OAuthAccount[];
	@IsDefined() @MinLength(1) full_name: string;
	/** @format email */
	@IsDefined() @IsEmail({ require_tld: false }) @MinLength(1) email_address: string;
	@IsOptional() email_notifications_enabled?: boolean;
	@IsOptional() is_email_address_verified?: boolean;
	@IsOptional() verify_email_address_token?: string | null;
	/** @format date-time */
	@IsOptional() @IsDate() verify_email_address_token_expiration?: string;
	@IsOptional() is_active?: boolean;
	@IsOptional() roles?: string[];
	/** @format date-time */
	@IsOptional() @IsDate() created_utc?: string;
	/** @format date-time */
	@IsOptional() @IsDate() updated_utc?: string;
}

export class UserDescription {
	constructor(description: string) {
		this.description = description;
	}

	@IsOptional() email_address?: string | null;
	@IsDefined() @MinLength(1) description: string;
	@IsOptional() @ValidateNested() data?: Record<string, unknown>;
}

export class ViewOrganization {
	@IsOptional() @IsMongoId() id?: string;
	/** @format date-time */
	@IsOptional() @IsDate() created_utc?: string;
	@IsOptional() name?: string;
	@IsOptional() @IsMongoId() plan_id?: string;
	@IsOptional() plan_name?: string;
	@IsOptional() plan_description?: string;
	@IsOptional() 'card_last4'?: string | null;
	/** @format date-time */
	@IsOptional() @IsDate() subscribe_date?: string | null;
	/** @format date-time */
	@IsOptional() @IsDate() billing_change_date?: string | null;
	@IsOptional() @IsMongoId() billing_changed_by_user_id?: string | null;
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
	@IsOptional() @IsNumber() billing_price?: number;
	/** @format int32 */
	@IsOptional() @IsInt() max_events_per_month?: number;
	/** @format int32 */
	@IsOptional() @IsInt() bonus_events_per_month?: number;
	/** @format date-time */
	@IsOptional() @IsDate() bonus_expiration?: string | null;
	/** @format int32 */
	@IsOptional() @IsInt() retention_days?: number;
	@IsOptional() is_suspended?: boolean;
	@IsOptional() suspension_code?: string | null;
	@IsOptional() suspension_notes?: string | null;
	/** @format date-time */
	@IsOptional() @IsDate() suspension_date?: string | null;
	@IsOptional() has_premium_features?: boolean;
	/** @format int32 */
	@IsOptional() @IsInt() max_users?: number;
	/** @format int32 */
	@IsOptional() @IsInt() max_projects?: number;
	/** @format int64 */
	@IsOptional() @IsInt() project_count?: number;
	/** @format int64 */
	@IsOptional() @IsInt() stack_count?: number;
	/** @format int64 */
	@IsOptional() @IsInt() event_count?: number;
	@IsOptional() @ValidateNested() invites?: Invite[];
	@IsOptional() @ValidateNested() usage_hours?: UsageHourInfo[];
	@IsOptional() @ValidateNested() usage?: UsageInfo[];
	@IsOptional() @ValidateNested() data?: Record<string, unknown>;
	@IsOptional() is_throttled?: boolean;
	@IsOptional() is_over_monthly_limit?: boolean;
	@IsOptional() is_over_request_limit?: boolean;
}

export class ViewProject {
	@IsOptional() @IsMongoId() id?: string;
	/** @format date-time */
	@IsOptional() @IsDate() created_utc?: string;
	@IsOptional() @IsMongoId() organization_id?: string;
	@IsOptional() organization_name?: string;
	@IsOptional() name?: string;
	@IsOptional() delete_bot_data_enabled?: boolean;
	@IsOptional() @ValidateNested() data?: Record<string, unknown>;
	@IsOptional() promoted_tabs?: string[];
	@IsOptional() is_configured?: boolean | null;
	/** @format int64 */
	@IsOptional() @IsInt() stack_count?: number;
	/** @format int64 */
	@IsOptional() @IsInt() event_count?: number;
	@IsOptional() has_premium_features?: boolean;
	@IsOptional() has_slack_integration?: boolean;
	@IsOptional() @ValidateNested() usage_hours?: UsageHourInfo[];
	@IsOptional() @ValidateNested() usage?: UsageInfo[];
}

export class ViewToken {
	@IsOptional() @IsMongoId() id?: string;
	@IsOptional() @IsMongoId() organization_id?: string;
	@IsOptional() @IsMongoId() project_id?: string;
	@IsOptional() @IsMongoId() user_id?: string | null;
	@IsOptional() @IsMongoId() default_project_id?: string | null;
	@IsOptional() scopes?: string[];
	/** @format date-time */
	@IsOptional() @IsDate() expires_utc?: string | null;
	@IsOptional() notes?: string | null;
	@IsOptional() is_disabled?: boolean;
	@IsOptional() is_suspended?: boolean;
	/** @format date-time */
	@IsOptional() @IsDate() created_utc?: string;
	/** @format date-time */
	@IsOptional() @IsDate() updated_utc?: string;
}

export class ViewUser {
	@IsOptional() @IsMongoId() id?: string;
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
	constructor(url: string, event_types: string[], version: string) {
		this.url = url;
		this.event_types = event_types;
		this.version = version;
	}

	@IsOptional() @IsMongoId() id?: string;
	@IsOptional() @IsMongoId() organization_id?: string;
	@IsOptional() @IsMongoId() project_id?: string;
	@IsDefined() @IsUrl() @MinLength(1) url: string;
	@IsDefined() event_types: string[];
	@IsOptional() is_enabled?: boolean;
	@IsDefined() @MinLength(1) version: string;
	/** @format date-time */
	@IsOptional() @IsDate() created_utc?: string;
}
