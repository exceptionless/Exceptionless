using Exceptionless.EmailTemplates.Models;
using Microsoft.AspNetCore.Components;

namespace Exceptionless.EmailTemplates.Components;

public abstract class EmailTemplateComponent<TModel> : ComponentBase where TModel : EmailTemplate
{
    [Parameter, EditorRequired]
    public required TModel Model { get; set; }
}
