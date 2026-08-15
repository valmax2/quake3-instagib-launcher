namespace Quake3InstaGibLauncher.Core.Services;

/// <summary>Sollevata quando un parametro scelto dall'utente non e' sicuro o valido da passare al motore di gioco.</summary>
public sealed class LaunchValidationException : Exception
{
    public LaunchValidationException(string message) : base(message) { }
}
