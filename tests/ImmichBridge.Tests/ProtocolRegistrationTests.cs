namespace ImmichBridge.Tests;

public sealed class ProtocolRegistrationTests
{
    [Fact]
    public void BuildCommand_QuotesExecutableAndProtocolArgument()
    {
        var command = WindowsProtocolRegistrar.BuildCommand(@"C:\Program Files\Immich Bridge\ImmichBridge.exe");

        Assert.Equal("\"C:\\Program Files\\Immich Bridge\\ImmichBridge.exe\" \"%1\"", command);
    }

    [Fact]
    public void ParseExecutablePath_ReadsPathBackFromCommand()
    {
        var executablePath = @"C:\Users\me\AppData\Local\Programs\ImmichBridge\ImmichBridge.exe";

        var parsed = WindowsProtocolRegistrar.ParseExecutablePath(WindowsProtocolRegistrar.BuildCommand(executablePath));

        Assert.Equal(executablePath, parsed);
    }

    [Fact]
    public void ParseExecutablePath_RejectsUnquotedCommand()
    {
        Assert.Null(WindowsProtocolRegistrar.ParseExecutablePath(@"C:\ImmichBridge.exe %1"));
    }
}
