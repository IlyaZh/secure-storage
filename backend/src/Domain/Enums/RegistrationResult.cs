namespace SecureStorage.Domain.Enums;

public enum RegistrationResult
{
    Success,
    AlreadyRegistered,
    InviteNotFoundOrUsed,
    EmailMismatch
}