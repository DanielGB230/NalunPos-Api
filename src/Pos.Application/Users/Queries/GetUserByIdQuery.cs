using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Users.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Users.Queries;

[HasPermission(Permissions.Users.View)]
public record GetUserByIdQuery(Guid Id) : IQuery<Result<UserDto>>;

public class GetUserByIdQueryHandler : IQueryHandler<GetUserByIdQuery, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;

    public GetUserByIdQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<Result<UserDto>> HandleAsync(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user == null)
        {
            return Result.Fail<UserDto>(DomainError.NotFound("User.NotFound", $"No se encontró el usuario con el ID '{request.Id}'."));
        }

        return Result.Ok(UserDto.FromEntity(user));
    }
}
