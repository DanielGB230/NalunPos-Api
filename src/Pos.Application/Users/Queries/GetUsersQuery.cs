using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Users.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.Users.Queries;

public record GetUsersQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    bool? IsActiveOnly = null
) : IQuery<PagedResult<UserDto>>;

public class GetUsersQueryHandler : IQueryHandler<GetUsersQuery, PagedResult<UserDto>>
{
    private readonly IUserRepository _userRepository;

    public GetUsersQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<PagedResult<UserDto>> HandleAsync(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _userRepository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            request.SearchTerm,
            request.IsActiveOnly,
            cancellationToken);

        var dtos = items.Select(UserDto.FromEntity).ToList();

        return new PagedResult<UserDto>(dtos, request.PageNumber, request.PageSize, totalCount);
    }
}
