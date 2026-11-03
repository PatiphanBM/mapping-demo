using System.Net;
using System.Net.Sockets;

namespace MappingDemo.Shared.Contracts;

public static class ApiDestinationProfileNetworkPolicy
{
    public static bool TryValidateOrigin(
        string value,
        out string diagnosticCode,
        out string diagnosticMessage)
    {
        diagnosticCode = string.Empty;
        diagnosticMessage = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            diagnosticCode = "InvalidProfileBaseUrl";
            diagnosticMessage = "Profile base URL must be an absolute HTTPS origin.";
            return false;
        }
        if (uri.IsLoopback ||
            IPAddress.TryParse(uri.Host, out _) ||
            uri.HostNameType != UriHostNameType.Dns)
        {
            diagnosticCode = "InvalidProfileHost";
            diagnosticMessage =
                "Profile host must be a non-loopback DNS name.";
            return false;
        }
        if (!uri.IsDefaultPort || uri.Port != 443)
        {
            diagnosticCode = "ProfilePortNotAllowed";
            diagnosticMessage = "Profile base URL must use approved port 443.";
            return false;
        }
        if (uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            diagnosticCode = "InvalidProfileUrl";
            diagnosticMessage =
                "Profile base URL cannot include path, query, fragment or user information.";
            return false;
        }
        return true;
    }

    public static async ValueTask<IReadOnlyList<IPAddress>>
        ResolveAndValidateAsync(
            string host,
            CancellationToken cancellationToken,
            Func<string, CancellationToken, ValueTask<IPAddress[]>>? resolver = null)
    {
        var addresses = resolver is null
            ? await Dns.GetHostAddressesAsync(host, cancellationToken)
            : await resolver(host, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(IsForbiddenAddress))
        {
            throw new InvalidOperationException(
                "Profile endpoint DNS resolution is not allowed by network policy.");
        }
        return addresses;
    }

    public static bool IsForbiddenAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] is 0 or 10 or 127 ||
                bytes[0] >= 224 ||
                (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) ||
                (bytes[0] == 169 && bytes[1] == 254) ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                (bytes[0] == 192 && bytes[1] == 0) ||
                (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) ||
                (bytes[0] == 198 && bytes[1] is 18 or 19) ||
                (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) ||
                (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113);
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return true;
        }
        var ipv6 = address.GetAddressBytes();
        return address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.IPv6None) ||
            address.Equals(IPAddress.IPv6Loopback) ||
            address.IsIPv6LinkLocal ||
            address.IsIPv6Multicast ||
            address.IsIPv6SiteLocal ||
            (ipv6[0] & 0xfe) == 0xfc ||
            (ipv6[0] == 0x20 && ipv6[1] == 0x01 &&
             ipv6[2] == 0x0d && ipv6[3] == 0xb8);
    }

    public static SocketsHttpHandler CreateSocketsHttpHandler() => new()
    {
        AllowAutoRedirect = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(1),
        ConnectCallback = ConnectAsync
    };

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await ResolveAndValidateAsync(
            context.DnsEndPoint.Host,
            cancellationToken);
        Exception? lastFailure = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(
                address.AddressFamily,
                SocketType.Stream,
                ProtocolType.Tcp)
            {
                NoDelay = true
            };
            try
            {
                await socket.ConnectAsync(
                    address,
                    context.DnsEndPoint.Port,
                    cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception exception) when (
                exception is SocketException or OperationCanceledException)
            {
                socket.Dispose();
                lastFailure = exception;
                if (exception is OperationCanceledException)
                {
                    throw;
                }
            }
        }
        throw new HttpRequestException(
            "Profile endpoint connection failed.",
            lastFailure);
    }
}
