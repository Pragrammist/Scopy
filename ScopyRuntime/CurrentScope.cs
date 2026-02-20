namespace ScopyRuntime;



//TODO: do it need/possible make custom function call in CurrentScope.







public partial class CurrentScope : IDisposable
{
    private Guid? _scopeId;
    
    string Name { set; get; }
    
    Guid ScopeId => _scopeId ??= Guid.NewGuid();


    static AsyncLocal<CurrentScope>? _currentAsyncLocal;

    static AsyncLocal<CurrentScope> CurrentAsyncLocal => _currentAsyncLocal ??= new AsyncLocal<CurrentScope>();

    static CurrentScope Current
        => CurrentAsyncLocal.Value!;




    
    readonly CurrentScope? _parent;


    static CurrentScope()
    {
        InitGlobalScope();
    }

    static void InitGlobalScope()
    {
        Push();
    }
    

    CurrentScope(CurrentScope? parent, string name = "Scope")
    {
        _parent = parent;
        Name = name;
    }

    public static void Push(string name = "Scope")
    {
        var scope = new CurrentScope(CurrentAsyncLocal.Value, name);
        CurrentAsyncLocal.Value = scope;
    }
    public void Dispose()
    {
        if(_parent is not null)
            CurrentAsyncLocal.Value = _parent;
    }

    public static void Pop()
    {
        Current.Dispose();
    }
}



public partial class CurrentScope
{
    readonly Dictionary<string, object> _values = new();


    static string GetNameForType(Type type) => type.FullName ?? type.Name;

    public static void Provide<T>(T value) where T : class => Provide(GetNameForType(typeof(T)), value);
    
    public static void Provide<T>(string name, T value) where T : class
    {
        Current._values.TryAdd(name, value);
    }
    
    public static T Resolve<T>() where T : class => Resolve<T>(GetNameForType(typeof(T)));
    
    
    public static T Resolve<T>(string name) where T : class
    {
        var scope = Current;

        while (scope != null)
        {
            if (scope._values.TryGetValue(name, out var value))
                return (T)value;

            scope = scope._parent;
        }

        throw new InvalidOperationException($"Context value not found: {GetNameForType(typeof(T))}");
    }
}




