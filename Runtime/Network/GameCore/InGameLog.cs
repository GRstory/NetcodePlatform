public class InGameLog
{
    public InGameLog(string text, ELogLevel logLevel = ELogLevel.Info)
    {
        LogText = text;
        LogLevel = logLevel;
    }
    public string LogText;
    public ELogLevel LogLevel;
}

public enum ELogLevel
{
    Info, Warn, Error, SystemInfo
}