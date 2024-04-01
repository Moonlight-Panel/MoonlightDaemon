namespace MoonlightDaemon.App.Exceptions;

public class UnsafeFileAccessException : Exception
{
    public UnsafeFileAccessException()
    {
    }

    public UnsafeFileAccessException(string message) : base(message)
    {
    }

    public UnsafeFileAccessException(string message, Exception inner) : base(message, inner)
    {
    }
}