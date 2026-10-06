using {{App}}.Application.Features.{{Plural}}.Actions.{{ActionName}}{{Entity}};
using {{App}}.Application.Features.{{Plural}}.Dtos;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace {{App}}.Api.Controllers.{{Plural}};

public sealed partial class {{Plural}}Controller
{
    [Http{{HttpVerb}}("{id:guid}/{{ActionRoute}}")]
    [ProducesResponseType(typeof({{Entity}}Dto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<{{Entity}}Dto>> {{ActionName}}(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new {{ActionName}}{{Entity}}{{RequestKind}}(id), cancellationToken));
}
