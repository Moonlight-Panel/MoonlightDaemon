namespace MoonlightDaemon.App.Exceptions;

public class IllegalStateException : Exception
{
    public IllegalStateException()
    {
    }

    public IllegalStateException(string message) : base(message)
    {
    }

    public IllegalStateException(string message, Exception inner) : base(message, inner)
    {
    }
}