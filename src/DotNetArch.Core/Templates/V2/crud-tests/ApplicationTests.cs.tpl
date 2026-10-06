using {{App}}.Application.Common.Exceptions;
using {{App}}.Application.Features.{{Plural}}.Commands.Create{{Entity}};
using {{App}}.Application.Features.{{Plural}}.Commands.Delete{{Entity}};
using {{App}}.Application.Features.{{Plural}}.Commands.Update{{Entity}};
using {{App}}.Application.Features.{{Plural}}.Queries.Get{{Entity}}ById;
using {{App}}.Application.Features.{{Plural}}.Queries.Get{{Plural}};
using {{App}}.Application.Tests.Support;
using {{App}}.Domain.Entities;

namespace {{App}}.Application.Tests.Features.{{Plural}};

public class {{Entity}}HandlerTests
{
    private static readonly FixedTimeProvider Clock = FixedTimeProvider.At(2026, 1, 1);

    private static {{Entity}} Seed(FakeUnitOfWork unitOfWork, string name = "Seed")
    {
        var entity = {{Entity}}.Create(name, Clock.GetUtcNow().UtcDateTime);
        unitOfWork.Fake<{{Entity}}>().Items.Add(entity);
        return entity;
    }

    [Fact]
    public async Task Create_adds_the_entity_and_saves_once()
    {
        var unitOfWork = new FakeUnitOfWork();

        var dto = await new Create{{Entity}}CommandHandler(unitOfWork, Clock).Handle(new Create{{Entity}}Command("Widget"), CancellationToken.None);

        Assert.Equal("Widget", dto.Name);
        Assert.Single(unitOfWork.Fake<{{Entity}}>().Items);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_renames_the_entity()
    {
        var unitOfWork = new FakeUnitOfWork();
        var entity = Seed(unitOfWork);

        var dto = await new Update{{Entity}}CommandHandler(unitOfWork, Clock).Handle(new Update{{Entity}}Command(entity.Id, "Renamed"), CancellationToken.None);

        Assert.Equal("Renamed", dto.Name);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_of_a_missing_entity_throws_not_found()
    {
        var handler = new Update{{Entity}}CommandHandler(new FakeUnitOfWork(), Clock);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new Update{{Entity}}Command(Guid.NewGuid(), "x"), CancellationToken.None));
    }

    [Fact]
    public async Task Delete_removes_the_entity()
    {
        var unitOfWork = new FakeUnitOfWork();
        var entity = Seed(unitOfWork);

        await new Delete{{Entity}}CommandHandler(unitOfWork).Handle(new Delete{{Entity}}Command(entity.Id), CancellationToken.None);

        Assert.Empty(unitOfWork.Fake<{{Entity}}>().Items);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Delete_of_a_missing_entity_throws_not_found()
    {
        var handler = new Delete{{Entity}}CommandHandler(new FakeUnitOfWork());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new Delete{{Entity}}Command(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GetById_returns_the_dto_or_throws_not_found()
    {
        var unitOfWork = new FakeUnitOfWork();
        var entity = Seed(unitOfWork, "Found");
        var handler = new Get{{Entity}}ByIdQueryHandler(unitOfWork);

        Assert.Equal("Found", (await handler.Handle(new Get{{Entity}}ByIdQuery(entity.Id), CancellationToken.None)).Name);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new Get{{Entity}}ByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Paged_query_returns_the_requested_page()
    {
        var unitOfWork = new FakeUnitOfWork();
        Seed(unitOfWork, "A");
        Seed(unitOfWork, "B");
        Seed(unitOfWork, "C");

        var page = await new Get{{Plural}}QueryHandler(unitOfWork).Handle(new Get{{Plural}}Query(PageNumber: 2, PageSize: 2), CancellationToken.None);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Single(page.Items);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("ok", true)]
    public void Create_validator_requires_a_name(string name, bool valid) =>
        Assert.Equal(valid, new Create{{Entity}}CommandValidator().Validate(new Create{{Entity}}Command(name)).IsValid);

    [Fact]
    public void Update_validator_requires_id_and_name() =>
        Assert.False(new Update{{Entity}}CommandValidator().Validate(new Update{{Entity}}Command(Guid.Empty, "")).IsValid);

    [Theory]
    [InlineData(0, 20, false)]
    [InlineData(1, 101, false)]
    [InlineData(1, 20, true)]
    public void Paged_query_validator_bounds_the_page(int pageNumber, int pageSize, bool valid) =>
        Assert.Equal(valid, new Get{{Plural}}QueryValidator().Validate(new Get{{Plural}}Query(pageNumber, pageSize)).IsValid);
}
