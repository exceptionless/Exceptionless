import { IsString } from 'class-validator';

export class CustomDateRange {
    @IsString()
    end?: string;

    @IsString()
    start?: string;
}
