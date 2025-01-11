using UnityEngine.UIElements;

public interface IUISetup<TContext>
{
    void Setup(UIDocument ui, TContext context);
}

public interface IUISetup
{
    void Setup(UIDocument ui);
}

public interface IUICleanup
{
    void Cleanup(UIDocument ui);
}
