using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Runtime.InteropServices;

namespace LibationAvalonia;

/// <summary>
/// Tints the Windows title bar to match the app theme.
/// <para/>
/// The caption bar is drawn by the OS, not by Avalonia, so no theme resource reaches
/// it. Windows 11 (build 22000+) exposes it through DwmSetWindowAttribute. Everything
/// here is best-effort: on Windows 10, Linux, macOS, or any failure, the call is
/// skipped and the OS default caption is used.
/// </summary>
public static class WindowChrome
{
	private const int DWMWA_CAPTION_COLOR = 35;
	private const int DWMWA_TEXT_COLOR = 36;
	private const int DWMWA_BORDER_COLOR = 34;

	[DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

	/// <summary>
	/// Applies caption, caption-text and border colours from the current theme.
	/// Safe to call on any platform and at any time after the window has a handle.
	/// </summary>
	public static void ApplyThemeToTitleBar(Window window)
	{
		if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
			return;

		try
		{
			if (window.TryGetPlatformHandle()?.Handle is not IntPtr hwnd || hwnd == IntPtr.Zero)
				return;

			if (TryGetBrushColor(window, "WindowCaption", out var caption))
				SetAttribute(hwnd, DWMWA_CAPTION_COLOR, caption);

			if (TryGetBrushColor(window, "WindowCaptionText", out var captionText))
				SetAttribute(hwnd, DWMWA_TEXT_COLOR, captionText);

			if (TryGetBrushColor(window, "WindowOutline", out var border))
				SetAttribute(hwnd, DWMWA_BORDER_COLOR, border);
		}
		catch (Exception ex)
		{
			// Never let cosmetic chrome break a window from opening.
			Serilog.Log.Logger.Debug(ex, "Could not apply theme colors to the title bar");
		}
	}

	private static bool TryGetBrushColor(Window window, string resourceKey, out Color color)
	{
		color = default;

		if (window.TryFindResource(resourceKey, window.ActualThemeVariant, out var resource)
			&& resource is ISolidColorBrush brush)
		{
			color = brush.Color;
			return true;
		}

		return false;
	}

	/// <summary> DWM expects 0x00BBGGRR, which is byte-reversed from the usual RGB order. </summary>
	private static void SetAttribute(IntPtr hwnd, int attribute, Color color)
	{
		var value = color.R | (color.G << 8) | (color.B << 16);
		DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));
	}
}
