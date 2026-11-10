---
name: maui-emulator-networking
description: Configures IP 10.0.2.2 and SSL certificate bypass to enable Android Emulators to communicate with local development APIs.
---

# Android Emulator Networking & Localhost Workaround

Derived from `davidortinau/maui-skills/maui-aspire`. Addresses the most frequent network failure when running MAUI mobile clients against local Web APIs.

## 1. The Core Issue: Why `localhost` Fails on Android Emulators
- When running inside the Android Emulator, `localhost` (or `127.0.0.1`) refers to the loopback interface of the **virtual device itself**, not the host machine running Visual Studio or ASP.NET Core.
- Calling `https://localhost:5001/api/rooms` from the Android Emulator throws `HttpRequestException: Connection refused`.

## 2. Standardized Configuration (Example)
The Android Emulator maps host IP `127.0.0.1` to internal alias **`10.0.2.2`**.

Example API client configuration (ensure the port matches the active profile in `RoomToRentRadar.Api/Properties/launchSettings.json`):

```csharp
public static class ApiConfiguration
{
    public static string BaseUrl =>
        DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5000/api"      // Android Emulator host loopback (adjust port as configured)
            : "http://localhost:5000/api";    // Windows Desktop / iOS Simulator
}
```

## 3. Handling Development SSL Certificates
When running local HTTPS with .NET self-signed development certificates:
```csharp
public static HttpMessageHandler GetInsecureHandler()
{
#if DEBUG
    var handler = new HttpClientHandler();
    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
    {
        if (cert?.Issuer.Equals("CN=localhost") == true) return true;
        return errors == System.Net.Security.SslPolicyErrors.None;
    };
    return handler;
#else
    return new HttpClientHandler();
#endif
}
```
