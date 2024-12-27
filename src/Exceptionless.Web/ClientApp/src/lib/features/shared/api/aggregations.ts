import type {
    BucketAggregate,
    DateHistogramBucket,
    ExtendedStatsAggregate,
    IAggregate,
    IBucket,
    KeyedBucket,
    MultiBucketAggregate,
    ObjectValueAggregate,
    PercentileItem,
    PercentilesAggregate,
    SingleBucketAggregate,
    StatsAggregate,
    TermsAggregate,
    TopHitsAggregate,
    ValueAggregate
} from '../models';

export function average(aggregations: Record<string, IAggregate> | undefined, key: string): undefined | ValueAggregate {
    return tryGet<ValueAggregate>(aggregations, key);
}

export function cardinality(aggregations: Record<string, IAggregate> | undefined, key: string): undefined | ValueAggregate {
    return tryGet<ValueAggregate>(aggregations, key);
}

export function dateHistogram(aggregations: Record<string, IAggregate> | undefined, key: string): MultiBucketAggregate<DateHistogramBucket> | undefined {
    return getMultiBucketAggregate<DateHistogramBucket>(aggregations, key);
}

export function extendedStats(aggregations: Record<string, IAggregate> | undefined, key: string): ExtendedStatsAggregate | undefined {
    return tryGet<ExtendedStatsAggregate>(aggregations, key);
}

export function geoHash(aggregations: Record<string, IAggregate> | undefined, key: string): MultiBucketAggregate<KeyedBucket<string>> | undefined {
    return getMultiKeyedBucketAggregate<string>(aggregations, key);
}

export function getPercentile(agg: PercentilesAggregate, percentile: number): PercentileItem | undefined {
    return agg.items.find((i) => i.percentile === percentile); // Checked
}

export function max<T = number>(aggregations: Record<string, IAggregate> | undefined, key: string): undefined | ValueAggregate<T> {
    return tryGet<ValueAggregate<T>>(aggregations, key);
}

export function metric(aggregations: Record<string, IAggregate> | undefined, key: string): ObjectValueAggregate | undefined {
    const valueMetric = tryGet<ValueAggregate>(aggregations, key);
    if (valueMetric) {
        return <ObjectValueAggregate>{
            data: valueMetric.data,
            value: valueMetric.value
        };
    }

    return tryGet<ObjectValueAggregate>(aggregations, key);
}

export function min<T = number>(aggregations: Record<string, IAggregate> | undefined, key: string): undefined | ValueAggregate<T> {
    return tryGet<ValueAggregate<T>>(aggregations, key);
}

export function missing(aggregations: Record<string, IAggregate> | undefined, key: string): SingleBucketAggregate | undefined {
    return tryGet<SingleBucketAggregate>(aggregations, key);
}

export function percentiles(aggregations: Record<string, IAggregate> | undefined, key: string): PercentilesAggregate | undefined {
    return tryGet<PercentilesAggregate>(aggregations, key);
}

export function stats(aggregations: Record<string, IAggregate> | undefined, key: string): StatsAggregate | undefined {
    return tryGet<StatsAggregate>(aggregations, key);
}

export function sum(aggregations: Record<string, IAggregate> | undefined, key: string): undefined | ValueAggregate {
    return tryGet<ValueAggregate>(aggregations, key);
}

export function terms<TKey = string>(aggregations: Record<string, IAggregate> | undefined, key: string): TermsAggregate<TKey> | undefined {
    const bucket = tryGet<BucketAggregate>(aggregations, key);
    if (!bucket) {
        return;
    }

    return <TermsAggregate<TKey>>{
        buckets: getKeyedBuckets<TKey>(bucket.items),
        data: bucket.data
    };
}

export function topHits<T = unknown>(aggregations: Record<string, IAggregate>): TopHitsAggregate<T> | undefined {
    return tryGet<TopHitsAggregate<T>>(aggregations, 'tophits');
}

function getKeyedBuckets<TKey>(items: IBucket[]): KeyedBucket<TKey>[] {
    return items
        .filter((bucket): bucket is KeyedBucket<TKey> => 'key' in bucket)
        .map((bucket) => ({
            aggregations: bucket.aggregations,
            key: bucket.key, // NOTE: May have to convert to proper type
            key_as_string: bucket.key_as_string,
            total: bucket.total
        }));
}

function getMultiBucketAggregate<TBucket extends IBucket>(
    aggregations: Record<string, IAggregate> | undefined,
    key: string
): MultiBucketAggregate<TBucket> | undefined {
    const bucket = tryGet<BucketAggregate>(aggregations, key);
    if (!bucket) {
        return;
    }

    return <MultiBucketAggregate<TBucket>>{
        buckets: bucket.items,
        data: bucket.data
    };
}

function getMultiKeyedBucketAggregate<TKey>(
    aggregations: Record<string, IAggregate> | undefined,
    key: string
): MultiBucketAggregate<KeyedBucket<TKey>> | undefined {
    const bucket = tryGet<BucketAggregate>(aggregations, key);
    if (!bucket) {
        return;
    }

    return <MultiBucketAggregate<KeyedBucket<TKey>>>{
        buckets: getKeyedBuckets<TKey>(bucket.items),
        data: bucket.data
    };
}

function tryGet<TAggregate extends IAggregate>(aggregations: Record<string, IAggregate> | undefined, key: string): TAggregate | undefined {
    return aggregations?.[key] as TAggregate;
}
