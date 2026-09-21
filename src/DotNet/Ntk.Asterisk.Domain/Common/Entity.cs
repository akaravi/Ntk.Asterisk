namespace Ntk.Asterisk.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}

public interface IAggregateRoot
{
}

public abstract class Entity<TId> where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();

    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override int GetHashCode() => EqualityComparer<TId>.Default.GetHashCode(Id);
}
