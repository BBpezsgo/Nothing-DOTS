using UnityEngine.UIElements;

interface IUISetup<TContext>
{
    void Setup(UIDocument ui, TContext context);
}

interface IUICleanup
{
    void Cleanup(UIDocument ui);
}
