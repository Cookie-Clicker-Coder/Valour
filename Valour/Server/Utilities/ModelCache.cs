using System.Collections;
using Valour.Sdk.Extensions;
using Valour.Shared.Models;

namespace Valour.Server.Utilities;

/// <summary>
/// The ModelCache is used for
/// caching collections of models that are frequently accessed and updated.
/// It performs no allocations and protects the internal store.
/// </summary>
public class ModelCache<T> where T : ServerModel
{
    public IReadOnlyList<T> Values { get; private set; }
    public IReadOnlyDictionary<long, T> Lookup { get; private set; }
    
    private List<T> _cache;
    private Dictionary<long, T> _lookup;
    
    public ModelCache()
    {
        _cache = new();
        _lookup = new();
    }
    
    public ModelCache(List<T> initial)
    {
        _cache = initial;
        _lookup = _cache.ToDictionary(x => x.Id);
    }
    
    public void Add(T item)
    {
        _cache.Add(item);
        _lookup.Add(item.Id, item);
    }
    
    public void Remove(long id)
    {
        if (_lookup.TryGetValue(id, out var item))
        {
            _cache.Remove(item);
            _lookup.Remove(id);
        }
    }
    
    public void Update(T updated)
    {
        if (_lookup.TryGetValue(updated.Id, out var old))
        {
            updated.CopyAllTo(old);
        }
        else
        {
            Add(updated);
        }
    }
    
    public T Get(long id)
    {
        _lookup.TryGetValue(id, out var item);
        return item;
    }
}

public class OrderedModelCache<T> where T : ServerModel, IOrderedModel
{
    public IReadOnlyList<T> Values { get; private set; }
    public IReadOnlyDictionary<long, T> Lookup { get; private set; }
    
    private List<T> _cache;
    private Dictionary<long, T> _lookup;
    
    public OrderedModelCache()
    {
        _cache = new();
        _lookup = new();
    }
    
    public OrderedModelCache(List<T> initial)
    {
        _cache = initial;
        _lookup = _cache.ToDictionary(x => x.Id);
    }
    
    public void Add(T item)
    {
        _cache.Add(item);
        _lookup.Add(item.Id, item);
        _cache.Sort(item.Compare);
    }
    
    public void Remove(long id)
    {
        if (_lookup.TryGetValue(id, out var item))
        {
            _cache.Remove(item);
            _lookup.Remove(id);
        }
    }
    
    public void Update(T updated)
    {
        if (_lookup.TryGetValue(updated.Id, out var old))
        {
            updated.CopyAllTo(old);
            
            // check if the position has changed
            if (old.Position != updated.Position)
            {
                _cache.Sort(updated.Compare);
            }
        }
        else
        {
            Add(updated);
            _cache.Sort(updated.Compare);
        }
    }
    
    public T Get(long id)
    {
        _lookup.TryGetValue(id, out var item);
        return item;
    }
}