using Unity.Entities;

public enum SelectionStatus
{
    None,
    Candidate,
    Selected,
}

public struct SelectableUnit : IComponentData
{
    public SelectionStatus Status;
}
