using {{App}}.Application.Features.{{Plural}}.Actions.{{ActionName}}{{Entity}};
{{DtoUsing}}using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace {{App}}.Api.Controllers.{{Plural}};

public sealed partial class {{Plural}}Controller
{
    /// <summary>{{Summary}}</summary>
    [Http{{HttpVerb}}("{{ActionRoute}}")]{{AnonymousAttribute}}
{{ControllerBody}}
}
