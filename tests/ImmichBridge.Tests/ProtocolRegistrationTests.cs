namespace ImmichBridge.Tests;

public sealed class ProtocolRegistrationTests
{
    [Fact]
    public void BuildCommand_QuotesExecutableAndProtocolArgument()
    {
        var command = WindowsProtocolRegistrar.BuildCommand(@"C:\Program Files\Immich Bridge\ImmichBridge.exe");

        Assert.Equal("\"C:\\Program Files\\Immich Bridge\\ImmichBridge.exe\" \"%1\"", command);
    }
}
