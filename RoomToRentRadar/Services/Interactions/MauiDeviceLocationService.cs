using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

namespace RoomToRentRadar.Services.Interactions;

public sealed class MauiDeviceLocationService : IDeviceLocationService
{
    public async Task<DeviceLocationResult> GetCurrentLocationAsync(CancellationToken cancellationToken = default)
    {
#if WINDOWS
        Windows.Devices.Geolocation.Geolocator? geolocator = null;
        try
        {
            var accessStatus = await MainThread.InvokeOnMainThreadAsync(
                async () => await Windows.Devices.Geolocation.Geolocator.RequestAccessAsync());
            cancellationToken.ThrowIfCancellationRequested();

            geolocator = new Windows.Devices.Geolocation.Geolocator
            {
                DesiredAccuracy = Windows.Devices.Geolocation.PositionAccuracy.High
            };

            switch (accessStatus)
            {
                case Windows.Devices.Geolocation.GeolocationAccessStatus.Denied:
                    return geolocator.LocationStatus == Windows.Devices.Geolocation.PositionStatus.Disabled
                        ? new DeviceLocationResult(DeviceLocationStatus.LocationDisabled)
                        : new DeviceLocationResult(DeviceLocationStatus.PermissionDenied);

                case Windows.Devices.Geolocation.GeolocationAccessStatus.Unspecified:
                    return geolocator.LocationStatus == Windows.Devices.Geolocation.PositionStatus.Disabled
                        ? new DeviceLocationResult(DeviceLocationStatus.LocationDisabled)
                        : new DeviceLocationResult(DeviceLocationStatus.UnableToLocate);
            }

            if (geolocator.LocationStatus == Windows.Devices.Geolocation.PositionStatus.Disabled)
            {
                return new DeviceLocationResult(DeviceLocationStatus.LocationDisabled);
            }

            if (geolocator.LocationStatus == Windows.Devices.Geolocation.PositionStatus.NotAvailable)
            {
                return new DeviceLocationResult(DeviceLocationStatus.NotSupported);
            }

            var pos = await geolocator
                .GetGeopositionAsync(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(8))
                .AsTask(cancellationToken);

            var position = pos.Coordinate.Point.Position;
            return new DeviceLocationResult(
                DeviceLocationStatus.Success,
                position.Latitude,
                position.Longitude,
                pos.Coordinate.Accuracy);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return geolocator?.LocationStatus == Windows.Devices.Geolocation.PositionStatus.Disabled
                ? new DeviceLocationResult(DeviceLocationStatus.LocationDisabled)
                : new DeviceLocationResult(DeviceLocationStatus.PermissionDenied);
        }
        catch (Exception)
        {
            if (geolocator?.LocationStatus == Windows.Devices.Geolocation.PositionStatus.Disabled)
            {
                return new DeviceLocationResult(DeviceLocationStatus.LocationDisabled);
            }

            if (geolocator?.LocationStatus == Windows.Devices.Geolocation.PositionStatus.NotAvailable)
            {
                return new DeviceLocationResult(DeviceLocationStatus.NotSupported);
            }

            return new DeviceLocationResult(DeviceLocationStatus.UnableToLocate);
        }
#else
        try
        {
            var permission = await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }

                return status;
            });

            cancellationToken.ThrowIfCancellationRequested();
            if (permission != PermissionStatus.Granted)
            {
                return new DeviceLocationResult(DeviceLocationStatus.PermissionDenied);
            }

            var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(15))
            {
                RequestFullAccuracy = true
            };
            var initialFix = await Geolocation.Default.GetLocationAsync(request, cancellationToken);
            if (initialFix is null)
            {
                return new DeviceLocationResult(DeviceLocationStatus.UnableToLocate);
            }

            var bestLocation = await RefineLocationAsync(initialFix, cancellationToken);
            return new DeviceLocationResult(
                DeviceLocationStatus.Success,
                bestLocation.Latitude,
                bestLocation.Longitude,
                bestLocation.Accuracy);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FeatureNotEnabledException)
        {
            return new DeviceLocationResult(DeviceLocationStatus.LocationDisabled);
        }
        catch (PermissionException)
        {
            return new DeviceLocationResult(DeviceLocationStatus.PermissionDenied);
        }
        catch (FeatureNotSupportedException)
        {
            return new DeviceLocationResult(DeviceLocationStatus.NotSupported);
        }
        catch (Exception)
        {
            return new DeviceLocationResult(DeviceLocationStatus.UnableToLocate);
        }
#endif
    }

    public async Task<bool> OpenLocationSettingsAsync()
    {
        try
        {
#if WINDOWS
            return await Launcher.Default.OpenAsync(new Uri("ms-settings:privacy-location"));
#elif ANDROID
            try
            {
                var intent = new Android.Content.Intent(Android.Provider.Settings.ActionLocationSourceSettings);
                if (Platform.CurrentActivity is { } activity)
                {
                    activity.StartActivity(intent);
                    return true;
                }

                intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                Android.App.Application.Context.StartActivity(intent);
                return true;
            }
            catch
            {
                AppInfo.Current.ShowSettingsUI();
                return true;
            }
#else
            await Task.CompletedTask;
            AppInfo.Current.ShowSettingsUI();
            return true;
#endif
        }
        catch
        {
            return false;
        }
    }

#if !WINDOWS
    private static async Task<Location> RefineLocationAsync(Location initialLocation, CancellationToken cancellationToken)
    {
        if (initialLocation.Accuracy is not > 20 || Geolocation.Default.IsListeningForeground)
        {
            return initialLocation;
        }

        var bestLocation = initialLocation;
        var gate = new object();
        var preciseFixTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnLocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
        {
            var candidate = e.Location;
            if (candidate is null) return;

            var reachedTarget = false;
            lock (gate)
            {
                if (candidate.Accuracy is > 0 &&
                    (bestLocation.Accuracy is null or <= 0 || candidate.Accuracy.Value < bestLocation.Accuracy.Value))
                {
                    bestLocation = candidate;
                    if (candidate.Accuracy.Value <= 10)
                    {
                        reachedTarget = true;
                    }
                }
            }

            if (reachedTarget)
            {
                preciseFixTcs.TrySetResult();
            }
        }

        var started = false;
        Geolocation.Default.LocationChanged += OnLocationChanged;
        try
        {
            started = await Geolocation.Default.StartListeningForegroundAsync(
                new GeolocationListeningRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(1)));
            if (started)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(3.5));
                using var registration = timeoutCts.Token.Register(() => preciseFixTcs.TrySetResult());
                await preciseFixTcs.Task;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Keep the initial Best fix if foreground listening is unsupported on this platform.
        }
        finally
        {
            Geolocation.Default.LocationChanged -= OnLocationChanged;
            if (started)
            {
                try { Geolocation.Default.StopListeningForeground(); }
                catch { }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return bestLocation;
        }
    }
#endif
}
