namespace MoonlightDaemon.App.Exceptions;

public class BashException : Exception
{
    public BashException()
    {
    }

    public BashException(string message) : base(message)
    {
    }

    public BashException(string message, Exception inner) : base(message, inner)
    {
    }
}