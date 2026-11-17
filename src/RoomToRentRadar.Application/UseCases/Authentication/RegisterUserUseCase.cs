using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.Validators;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Application.UseCases.Authentication;

public sealed class RegisterUserUseCase : IRegisterUserUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public RegisterUserUseCase(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        TimeProvider? timeProvider = null)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RegisterUserResult> ExecuteAsync(
        RegisterUserCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var email = EmailAddress.Create(command.Email);
        var role = UserRoleExtensions.ParseSelfRegistrationRole(command.Role);
        PasswordPolicyValidator.Validate(command.Password);

        if (await _userRepository.ExistsByEmailAsync(email, cancellationToken))
        {
            throw new DuplicateEmailException();
        }

        var passwordHash = _passwordHasher.HashPassword(command.Password);
        var utcNow = _timeProvider.GetUtcNow();

        var user = User.RegisterLocalUser(email, passwordHash, role, utcNow);

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RegisterUserResult(
            user.Id,
            user.Email.Value,
            user.Role.ToRoleName(),
            user.CreatedAt);
    }
}
