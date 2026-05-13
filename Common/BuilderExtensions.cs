namespace Std.BuildTools.Common;

public static class BuilderExtensions
{
    public static TElement? Apply<TElement>(this TElement? obj, Func<TElement?, TElement?> func)
        where TElement : class =>
        obj == null
            ? null
            : func(obj);
}
