using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Kafka.Context.Streaming;

public sealed class StreamingQueryBuilder : IStreamingQueryBuilder
{
    public IStreamingQueryable<T> From<T>() where T : class
    {
        var plan = new StreamingQueryPlanBuilder();
        plan.AddSource(typeof(T));
        return new StreamingQueryable<T>(plan);
    }
}

internal sealed class StreamingQueryPlanBuilder
{
    private readonly List<Type> _sourceTypes = new();
    private readonly List<StreamingPredicate> _joinPredicates = new();
    private readonly List<StreamingPredicate> _wherePredicates = new();
    private readonly StreamingPredicateBuilder _predicateBuilder = new();
    private readonly StreamingGroupByClauseBuilder _groupByBuilder = new();

    private LambdaExpression? _selectSelector;
    private LambdaExpression? _groupByKeySelector;
    private LambdaExpression? _havingPredicate;
    private StreamingWindowSpec? _window;
    private StreamingGroupByClause? _groupByClause;

    public bool HasGroupBy { get; private set; }
    public bool HasAggregate { get; private set; }
    public bool HasHaving { get; private set; }

    public void AddSource(Type type)
    {
        if (!_sourceTypes.Contains(type))
            _sourceTypes.Add(type);
    }

    public void MarkGroupBy() => HasGroupBy = true;
    public void MarkAggregate() => HasAggregate = true;
    public void MarkHaving() => HasHaving = true;

    public void AddJoin(LambdaExpression predicate)
    {
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        _joinPredicates.Add(_predicateBuilder.Build(predicate));
    }

    public void AddWhere(LambdaExpression predicate)
    {
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        _wherePredicates.Add(_predicateBuilder.Build(predicate));
    }

    public void SetSelect(LambdaExpression selector)
    {
        if (selector is null) throw new ArgumentNullException(nameof(selector));
        _selectSelector = selector;
    }

    public void SetGroupBy(LambdaExpression keySelector)
    {
        if (keySelector is null) throw new ArgumentNullException(nameof(keySelector));
        _groupByKeySelector = keySelector;
        _groupByClause = _groupByBuilder.Build(keySelector);
    }

    public void SetHaving(LambdaExpression predicate)
    {
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        _havingPredicate = predicate;
    }

    public void SetWindow(StreamingWindowSpec window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public StreamingQueryPlan Build()
    {
        return new StreamingQueryPlan(_sourceTypes.ToArray(), HasGroupBy, HasAggregate, HasHaving)
        {
            JoinPredicates = _joinPredicates.ToArray(),
            WherePredicates = _wherePredicates.ToArray(),
            SelectSelector = _selectSelector,
            GroupByKeySelector = _groupByKeySelector,
            GroupByClause = _groupByClause,
            HavingPredicate = _havingPredicate,
            Window = _window,
        };
    }
}

internal abstract class StreamingQueryableBase : IStreamingQueryable
{
    protected StreamingQueryableBase(StreamingQueryPlanBuilder plan)
    {
        PlanBuilder = plan ?? throw new ArgumentNullException(nameof(plan));
    }

    internal StreamingQueryPlanBuilder PlanBuilder { get; }

    public StreamingQueryPlan Plan => PlanBuilder.Build();
}

internal sealed class StreamingQueryable<T> : StreamingQueryableBase, IStreamingQueryable<T>
{
    public StreamingQueryable(StreamingQueryPlanBuilder plan) : base(plan) { }
}

internal sealed class StreamingQueryable<T1, T2> : StreamingQueryableBase, IStreamingQueryable<T1, T2>
{
    public StreamingQueryable(StreamingQueryPlanBuilder plan) : base(plan) { }
}

internal sealed class StreamingQueryable<T1, T2, T3> : StreamingQueryableBase, IStreamingQueryable<T1, T2, T3>
{
    public StreamingQueryable(StreamingQueryPlanBuilder plan) : base(plan) { }
}

internal sealed class StreamingQueryable<T1, T2, T3, T4> : StreamingQueryableBase, IStreamingQueryable<T1, T2, T3, T4>
{
    public StreamingQueryable(StreamingQueryPlanBuilder plan) : base(plan) { }
}
