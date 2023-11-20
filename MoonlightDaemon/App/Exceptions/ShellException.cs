namespace MoonlightDaemon.App.Exceptions;

public class ShellException : Exception
{
    public ShellException()
    {
    }

    public ShellException(string message) : base(message)
    {
    }

    public ShellException(string message, Exception inner) : base(message, inner)
    {
    }
}