using Microsoft.AspNetCore.Components;

namespace Handal.Client.Components.UI;

public class CardBase : ComponentBase
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string? Class { get; set; }

    protected string CssClass => string.Join(" ", new[]
    {
        "rounded-lg border bg-card text-card-foreground shadow-sm",
        Class
    }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
