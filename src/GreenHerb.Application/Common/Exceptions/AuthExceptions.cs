namespace GreenHerb.Application.Common.Exceptions;

public sealed class UsernameAlreadyTakenException : Exception
{
    public UsernameAlreadyTakenException()
        : base("This username is already taken.")
    {
    }
}

public sealed class EmailAlreadyTakenException : Exception
{
    public EmailAlreadyTakenException()
        : base("An account with this email already exists.")
    {
    }
}

public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Incorrect email, username, or password.")
    {
    }
}

public sealed class InvalidGoogleTokenException : Exception
{
    public InvalidGoogleTokenException(string? reason = null)
        : base(string.IsNullOrWhiteSpace(reason) ? "Google sign-in failed." : $"Google sign-in failed. {reason}")
    {
    }
}

public sealed class GoogleEmailNotVerifiedException : Exception
{
    public GoogleEmailNotVerifiedException()
        : base("Your Google email address is not verified.")
    {
    }
}
