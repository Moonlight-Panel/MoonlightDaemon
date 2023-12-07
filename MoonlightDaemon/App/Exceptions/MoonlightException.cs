namespace MoonlightDaemon.App.Exceptions;

public class MoonlightException : Exception
{
    public MoonlightException()
    {
    }

    public MoonlightException(string message) : base(message)
    {
    }

    public MoonlightException(string message, Exception inner) : base(message, inner)
    {
    }
}