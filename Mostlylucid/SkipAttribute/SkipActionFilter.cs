namespace Mostlylucid.SkipAttribute;

public class SkipActionFilter(bool skipFilter) : Attribute
{
    public bool SkipFilter { get; } = skipFilter;
}