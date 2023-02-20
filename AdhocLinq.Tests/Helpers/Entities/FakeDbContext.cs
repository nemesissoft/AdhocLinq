using System.Collections;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq.Expressions;

namespace AdhocLinq.Tests.Helpers.Entities;

public class FakeDbSet<T> : IDbSet<T>
    where T : class
{
    private readonly IQueryable _query;

    public FakeDbSet() => _query = (Local = new ObservableCollection<T>()).AsQueryable();

    public ObservableCollection<T> Local { get; }

    Type IQueryable.ElementType => _query.ElementType;

    Expression IQueryable.Expression => _query.Expression;

    IQueryProvider IQueryable.Provider => _query.Provider;

    public T Add(T entity)
    {
        Local.Add(entity);
        return entity;
    }

    public T Attach(T entity)
    {
        Local.Add(entity);
        return entity;
    }

    public virtual T Find(params object[] keyValues) =>
        throw new NotSupportedException($"Derive from {nameof(FakeDbSet<string>)} and override {nameof(Find)} method");

    public T Remove(T entity)
    {
        Local.Remove(entity);
        return entity;
    }

    public T Create() => Activator.CreateInstance<T>();

    TDerivedEntity IDbSet<T>.Create<TDerivedEntity>() => Activator.CreateInstance<TDerivedEntity>();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => Local.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => Local.GetEnumerator();
}
