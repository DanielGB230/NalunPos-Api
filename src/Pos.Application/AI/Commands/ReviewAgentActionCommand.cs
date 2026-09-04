using FluentValidation;
using Pos.Application.AI.DTOs;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.AI.Commands;

public record ReviewAgentActionCommand(
    Guid RecordId,
    bool Approve
) : ICommand<AgentActionRecordDto>;

public class ReviewAgentActionCommandValidator : AbstractValidator<ReviewAgentActionCommand>
{
    public ReviewAgentActionCommandValidator()
    {
        RuleFor(x => x.RecordId)
            .NotEmpty().WithMessage("El ID del registro de propuesta es requerido.");
    }
}

public class ReviewAgentActionCommandHandler : ICommandHandler<ReviewAgentActionCommand, AgentActionRecordDto>
{
    private readonly IAgentActionRecordRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDispatcher _dispatcher;

    public ReviewAgentActionCommandHandler(
        IAgentActionRecordRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IDispatcher dispatcher)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task<AgentActionRecordDto> HandleAsync(ReviewAgentActionCommand request, CancellationToken cancellationToken)
    {
        var record = await _repository.GetByIdAsync(request.RecordId, cancellationToken)
            ?? throw new AgentActionRecordNotFoundException(request.RecordId);

        Guid reviewerUserId = _currentUserService.UserId
            ?? throw new UnauthorizedDomainException("Se requiere un usuario autenticado para revisar propuestas de agentes IA.");

        if (request.Approve)
        {
            record.Approve(reviewerUserId);
        }
        else
        {
            record.Reject(reviewerUserId);
        }

        _repository.Update(record);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in record.DomainEvents)
        {
            await _dispatcher.PublishAsync(domainEvent, cancellationToken);
        }
        record.ClearDomainEvents();

        return AgentActionRecordDto.FromEntity(record);
    }
}
