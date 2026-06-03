static class UnityUtils
{
#if UNITY_EDITOR
    public static void Quit() => UnityEditor.EditorApplication.ExitPlaymode();
#else
    public static void Quit() => UnityEngine.Application.Quit();
#endif
}
