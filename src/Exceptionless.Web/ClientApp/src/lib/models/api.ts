import { IsEmail, MinLength } from 'class-validator';
import type { LoginModel } from './api.generated';

export class Login implements LoginModel {
	@IsEmail({ require_tld: false })
	email!: string;

	@MinLength(6)
	password!: string;

	invite_token?: string;
}
