import type { IAggregate } from '.';

export interface AggregationsHelper {
    aggregations: Record<string, IAggregate>;
}

export interface BucketAggregate extends IAggregate {
    items: IBucket[];
    total: number;
}

export interface BucketAggregateBase extends AggregationsHelper, IAggregate {
    aggregations: Record<string, IAggregate>;
}

export interface BucketBase extends AggregationsHelper, IBucket {
    aggregations: Record<string, IAggregate>;
}

export interface DateHistogramBucket extends KeyedBucket<number> {
    date: string; // This needs to be converted to a date.
}

export interface ExtendedStatsAggregate extends StatsAggregate {
    std_deviation?: number;
    std_deviation_bounds?: StandardDeviationBounds;
    sum_of_squares?: number;
    variance?: number;
}

export interface IBucket {
    data?: Record<string, unknown>;
}

export interface KeyedBucket<T> extends BucketBase {
    key: T;
    key_as_string: string;
    total?: number;
}

export type MetricAggregateBase = IAggregate;

export interface MultiBucketAggregate<TBucket extends IBucket> extends BucketAggregateBase {
    buckets: TBucket[];
}

export interface ObjectValueAggregate extends MetricAggregateBase {
    value: unknown;
}

export interface PercentileItem {
    percentile: number;
    value?: number;
}

export interface PercentilesAggregate extends MetricAggregateBase {
    items: PercentileItem[];
}

export interface RangeBucket extends BucketBase {
    from?: number;
    from_as_string?: string;
    key: string;
    to?: number;
    to_as_string?: string;
    total: number;
}

export interface SingleBucketAggregate extends BucketAggregateBase {
    total: number;
}

export interface StandardDeviationBounds {
    lower?: number;
    upper?: number;
}

export interface StatsAggregate extends MetricAggregateBase {
    average?: number;
    count: number;
    max?: number;
    min?: number;
    sum?: number;
}

export type TermsAggregate<TKey> = MultiBucketAggregate<KeyedBucket<TKey>>;

export interface TopHitsAggregate<T> extends MetricAggregateBase {
    hits: T[];
    max_score?: number;
    total: number;
}

export interface ValueAggregate<T = number> extends MetricAggregateBase {
    value: T;
}
