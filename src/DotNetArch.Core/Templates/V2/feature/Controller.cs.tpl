using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace {{App}}.Api.Controllers.{{Plural}};

/// <summary>{{Summary}}</summary>
[ApiController]
[Route("api/{{RouteName}}")]
[Produces("application/json")]
public sealed partial class {{Plural}}Controller(ISender sender) : ControllerBase
{
}
