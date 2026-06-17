using Swyft.ServoProgrammer.Abstractions;

namespace Swyft.ServoProgrammer.Services;

/// <summary>
/// Factory that returns the appropriate <see cref="ISerialPortProvider"/> for the current operating system.
/// </summary>
public static class SerialPortProviderFactory
{
    private static ISerialPortProvider? _provider;

    public static ISerialPortProvider GetProvider()
    {
        if (_provider is not null)
            return _provider;

#if WINDOWS
        _provider = new WindowsSerialPortProvider();
#else
        _provider = new LinuxSerialPortProvider();
#endif

        return _provider;
    }
}
