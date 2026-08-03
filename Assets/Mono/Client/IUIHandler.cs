public interface IUISetup<TContext>
{
    void Setup(UIElementReference ui, TContext context);
}

public interface IUISetup
{
    void Setup(UIElementReference ui);
}

public interface IUICleanup
{
    void Cleanup(UIElementReference ui);
}
