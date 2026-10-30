using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace KarateTournamentApp.Services.Navigation
{
    public class WpfWindowService : IWindowService
    {
        private readonly Dictionary<Type, Func<FrameworkElement>> _viewFactories = new();
        private readonly Dictionary<string, WpfWindowHandle> _reusableWindows = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EventHandler?> _reusableWindowClosedHandlers = new(StringComparer.Ordinal);

        public void Register<TViewModel, TView>() where TView : FrameworkElement, new()
        {
            _viewFactories[typeof(TViewModel)] = () => new TView();
        }

        public IWindowHandle Show(object viewModel, WindowOptions? options = null)
        {
            var window = CreateWindow(viewModel, options);
            window.Show();
            return new WpfWindowHandle(window);
        }

        public IWindowHandle ShowOrUpdate(
            string key,
            object viewModel,
            WindowOptions? options = null,
            EventHandler? onClosed = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Необходимо указать ключ окна.", nameof(key));
            }

            if (_reusableWindows.TryGetValue(key, out var existingHandle) && !existingHandle.IsClosed)
            {
                UpdateWindow(existingHandle.Window, viewModel, options);
                _reusableWindowClosedHandlers[key] = onClosed;
                if (!existingHandle.Window.IsVisible)
                {
                    existingHandle.Window.Show();
                }
                existingHandle.Window.Activate();
                return existingHandle;
            }

            var window = CreateWindow(viewModel, options);
            var handle = new WpfWindowHandle(window);
            _reusableWindows[key] = handle;
            _reusableWindowClosedHandlers[key] = onClosed;
            handle.Closed += (_, _) =>
            {
                if (_reusableWindows.TryGetValue(key, out var currentHandle)
                    && ReferenceEquals(currentHandle, handle))
                {
                    _reusableWindows.Remove(key);
                    var closedHandler = _reusableWindowClosedHandlers[key];
                    _reusableWindowClosedHandlers.Remove(key);
                    closedHandler?.Invoke(handle, EventArgs.Empty);
                }
            };

            window.Show();
            return handle;
        }

        public bool? ShowDialog(object viewModel, WindowOptions? options = null, Action<IWindowHandle>? onOpened = null)
        {
            var window = CreateWindow(viewModel, options);
            onOpened?.Invoke(new WpfWindowHandle(window));
            return window.ShowDialog();
        }

        private Window CreateWindow(object viewModel, WindowOptions? options)
        {
            options ??= new WindowOptions();

            var window = new Window
            {
                Content = CreateView(viewModel)
            };

            ApplyOptions(window, options);
            return window;
        }

        private void UpdateWindow(Window window, object viewModel, WindowOptions? options)
        {
            window.Content = CreateView(viewModel);
            ApplyOptions(window, options ?? new WindowOptions());
        }

        private FrameworkElement CreateView(object viewModel)
        {
            if (!_viewFactories.TryGetValue(viewModel.GetType(), out var factory))
            {
                throw new InvalidOperationException($"Для модели представления {viewModel.GetType().Name} не зарегистрировано представление.");
            }

            var content = factory();
            content.DataContext = viewModel;
            return content;
        }

        private void ApplyOptions(Window window, WindowOptions options)
        {
            window.Title = options.Title ?? string.Empty;
            window.Width = options.Width ?? double.NaN;
            window.Height = options.Height ?? double.NaN;
            window.WindowState = options.SizeState == WindowSizeState.Maximized
                ? WindowState.Maximized
                : WindowState.Normal;
            window.WindowStyle = options.Borderless ? WindowStyle.None : WindowStyle.SingleBorderWindow;
            window.ResizeMode = options.Resizable ? ResizeMode.CanResize : ResizeMode.NoResize;
            window.Topmost = options.Topmost;
            window.WindowStartupLocation = options.StartupPosition switch
            {
                WindowStartupPosition.CenterScreen => WindowStartupLocation.CenterScreen,
                WindowStartupPosition.CenterOwner => WindowStartupLocation.CenterOwner,
                _ => WindowStartupLocation.Manual
            };

            if (options.FullScreenMonitor is WindowMonitor monitor)
            {
                var screen = GetMonitor(monitor);
                if (screen.HasValue)
                {
                    ApplyFullScreen(window, screen.Value);
                }
            }
        }

        private static MonitorBounds? GetMonitor(WindowMonitor monitor)
        {
            var screens = EnumerateMonitors();
            if (monitor == WindowMonitor.SecondaryIfAvailable)
            {
                foreach (var screen in screens)
                {
                    if (!screen.IsPrimary)
                    {
                        return screen;
                    }
                }

                return null;
            }

            foreach (var screen in screens)
            {
                if (screen.IsPrimary)
                {
                    return screen;
                }
            }

            return screens.Count > 0
                ? screens[0]
                : throw new InvalidOperationException("Не удалось найти доступный монитор.");
        }

        private static List<MonitorBounds> EnumerateMonitors()
        {
            var monitors = new List<MonitorBounds>();
            Exception? enumerationError = null;
            MonitorEnumProc callback = (monitorHandle, _, _, _) =>
            {
                var monitorInfo = new NativeMonitorInfo
                {
                    Size = Marshal.SizeOf<NativeMonitorInfo>()
                };

                if (!GetMonitorInfo(monitorHandle, ref monitorInfo))
                {
                    enumerationError = new Win32Exception(Marshal.GetLastWin32Error());
                    return false;
                }

                monitors.Add(new MonitorBounds(
                    monitorInfo.Monitor.Left,
                    monitorInfo.Monitor.Top,
                    monitorInfo.Monitor.Right - monitorInfo.Monitor.Left,
                    monitorInfo.Monitor.Bottom - monitorInfo.Monitor.Top,
                    (monitorInfo.Flags & MonitorInfoPrimary) != 0));
                return true;
            };

            var succeeded = EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
            GC.KeepAlive(callback);

            if (enumerationError != null)
            {
                throw new InvalidOperationException("Не удалось получить сведения о мониторе.", enumerationError);
            }

            if (!succeeded)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return monitors;
        }

        private static void ApplyFullScreen(Window window, MonitorBounds screen)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.WindowState = WindowState.Normal;
            window.WindowStyle = WindowStyle.None;
            window.ResizeMode = ResizeMode.NoResize;

            void PositionWindow()
            {
                var handle = new WindowInteropHelper(window).Handle;
                if (!SetWindowPos(
                        handle,
                        IntPtr.Zero,
                        screen.Left,
                        screen.Top,
                        screen.Width,
                        screen.Height,
                        SwpNoZOrder | SwpNoActivate))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }

            if (window.IsLoaded)
            {
                PositionWindow();
            }
            else
            {
                window.SourceInitialized += (_, _) => PositionWindow();
            }
        }

        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;
        private const uint MonitorInfoPrimary = 0x00000001;

        private readonly record struct MonitorBounds(int Left, int Top, int Width, int Height, bool IsPrimary);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMonitorInfo
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect Work;
            public uint Flags;
        }

        private delegate bool MonitorEnumProc(
            IntPtr monitorHandle,
            IntPtr deviceContext,
            IntPtr monitorRect,
            IntPtr data);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayMonitors(
            IntPtr deviceContext,
            IntPtr clipRect,
            MonitorEnumProc callback,
            IntPtr data);

        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitorHandle, ref NativeMonitorInfo monitorInfo);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(
            IntPtr windowHandle,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);
    }

    internal class WpfWindowHandle : IWindowHandle
    {
        internal Window Window { get; }
        internal bool IsClosed { get; private set; }

        public WpfWindowHandle(Window window)
        {
            Window = window;
            Window.Closed += (s, e) =>
            {
                IsClosed = true;
                Closed?.Invoke(this, EventArgs.Empty);
            };
        }

        public event EventHandler? Closed;

        public void Close() => Window.Close();
    }
}
