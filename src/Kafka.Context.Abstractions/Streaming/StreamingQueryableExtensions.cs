using System;
using System.Linq.Expressions;

namespace Kafka.Context.Streaming;

public static class StreamingQueryableExtensions
{
    public static IStreamingQueryable<TOuter, TInner> Join<TOuter, TInner>(
        this IStreamingQueryable<TOuter> outer,
        Expression<Func<TOuter, TInner, bool>> predicate)
        where TOuter : class
        where TInner : class
    {
        if (outer is not StreamingQueryableBase q)
            throw new InvalidOperationException("Unknown IStreamingQueryable implementation.");

        q.PlanBuilder.AddSource(typeof(TInner));
        q.PlanBuilder.AddJoin(predicate);
        return new StreamingQueryable<TOuter, TInner>(q.PlanBuilder);
    }

    public static IStreamingQueryable<T1, T2, T3> Join<T1, T2, T3>(
        this IStreamingQueryable<T1, T2> source,
        Expression<Func<T1, T2, T3, bool>> predicate)
        where T1 : class
        where T2 : class
        where T3 : class
    {
        if (source is not StreamingQueryableBase q)
            throw new InvalidOperationException("Unknown IStreamingQueryable implementation.");

        q.PlanBuilder.AddSource(typeof(T3));
        q.PlanBuilder.AddJoin(predicate);
        return new StreamingQueryable<T1, T2, T3>(q.PlanBuilder);
    }

    public static IStreamingQueryable<T1, T2, T3, T4> Join<T1, T2, T3, T4>(
        this IStreamingQueryable<T1, T2, T3> source,
        Expression<Func<T1, T2, T3, T4, bool>> predicate)
        where T1 : class
        where T2 : class
        where T3 : class
        where T4 : class
    {
        if (source is not StreamingQueryableBase q)
            throw new InvalidOperationException("Unknown IStreamingQueryable implementation.");

        q.PlanBuilder.AddSource(typeof(T4));
        q.PlanBuilder.AddJoin(predicate);
        return new StreamingQueryable<T1, T2, T3, T4>(q.PlanBuilder);
    }

    public static IStreamingQueryable<T> Where<T>(
        this IStreamingQueryable<T> source,
        Expression<Func<T, bool>> predicate)
        where T : class
    {
        if (source is StreamingQueryableBase q)
            q.PlanBuilder.AddWhere(predicate);
        return source;
    }

    public static IStreamingQueryable<T1, T2> Where<T1, T2>(
        this IStreamingQueryable<T1, T2> source,
        Expression<Func<T1, T2, bool>> predicate)
        where T1 : class
        where T2 : class
    {
        if (source is StreamingQueryableBase q)
            q.PlanBuilder.AddWhere(predicate);
        return source;
    }

    public static IStreamingQueryable<T1, T2, T3> Where<T1, T2, T3>(
        this IStreamingQueryable<T1, T2, T3> source,
        Expression<Func<T1, T2, T3, bool>> predicate)
        where T1 : class
        where T2 : class
        where T3 : class
    {
        if (source is StreamingQueryableBase q)
            q.PlanBuilder.AddWhere(predicate);
        return source;
    }

    public static IStreamingQueryable<T1, T2, T3, T4> Where<T1, T2, T3, T4>(
        this IStreamingQueryable<T1, T2, T3, T4> source,
        Expression<Func<T1, T2, T3, T4, bool>> predicate)
        where T1 : class
        where T2 : class
        where T3 : class
        where T4 : class
    {
        if (source is StreamingQueryableBase q)
            q.PlanBuilder.AddWhere(predicate);
        return source;
    }

    public static IStreamingQueryable<TResult> Select<T, TResult>(
        this IStreamingQueryable<T> source,
        Expression<Func<T, TResult>> selector)
        where T : class
        where TResult : class
    {
        if (source is not StreamingQueryableBase q)
            throw new InvalidOperationException("Unknown IStreamingQueryable implementation.");

        q.PlanBuilder.SetSelect(selector);
        return new StreamingQueryable<TResult>(q.PlanBuilder);
    }

    public static IStreamingQueryable<TResult> Select<T1, T2, TResult>(
        this IStreamingQueryable<T1, T2> source,
        Expression<Func<T1, T2, TResult>> selector)
        where T1 : class
        where T2 : class
        where TResult : class
    {
        if (source is not StreamingQueryableBase q)
            throw new InvalidOperationException("Unknown IStreamingQueryable implementation.");

        q.PlanBuilder.SetSelect(selector);
        return new StreamingQueryable<TResult>(q.PlanBuilder);
    }

    public static IStreamingQueryable<TResult> Select<T1, T2, T3, TResult>(
        this IStreamingQueryable<T1, T2, T3> source,
        Expression<Func<T1, T2, T3, TResult>> selector)
        where T1 : class
        where T2 : class
        where T3 : class
        where TResult : class
    {
        if (source is not StreamingQueryableBase q)
            throw new InvalidOperationException("Unknown IStreamingQueryable implementation.");

        q.PlanBuilder.SetSelect(selector);
        return new StreamingQueryable<TResult>(q.PlanBuilder);
    }

    public static IStreamingQueryable<TResult> Select<T1, T2, T3, T4, TResult>(
        this IStreamingQueryable<T1, T2, T3, T4> source,
        Expression<Func<T1, T2, T3, T4, TResult>> selector)
        where T1 : class
        where T2 : class
        where T3 : class
        where T4 : class
        where TResult : class
    {
        if (source is not StreamingQueryableBase q)
            throw new InvalidOperationException("Unknown IStreamingQueryable implementation.");

        q.PlanBuilder.SetSelect(selector);
        return new StreamingQueryable<TResult>(q.PlanBuilder);
    }

    public static IStreamingQueryable<T> GroupBy<T, TKey>(
        this IStreamingQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
        where T : class
    {
        if (source is not StreamingQueryableBase q)
            throw new InvalidOperationException("Unknown IStreamingQueryable implementation.");

        q.PlanBuilder.MarkGroupBy();
        q.PlanBuilder.SetGroupBy(keySelector);
        return source;
    }

    public static IStreamingQueryable<T> Aggregate<T>(
        this IStreamingQueryable<T> source,
        Expression<Func<T, T>> aggregator)
        where T : class
    {
        if (source is not StreamingQueryableBase q)
            throw new InvalidOperationException("Unknown IStreamingQueryable implementation.");

        q.PlanBuilder.MarkAggregate();
        return source;
    }

    public static IStreamingQueryable<T> Having<T>(
        this IStreamingQueryable<T> source,
        Expression<Func<T, bool>> predicate)
        where T : class
    {
        if (source is not StreamingQueryableBase q)
            throw new InvalidOperationException("Unknown IStreamingQueryable implementation.");

        q.PlanBuilder.MarkHaving();
        q.PlanBuilder.SetHaving(predicate);
        return source;
    }
}
