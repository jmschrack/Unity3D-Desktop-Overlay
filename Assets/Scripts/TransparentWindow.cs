using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

[RequireComponent(typeof(Camera))]
public class TransparentWindow : MonoBehaviour
{
	public static TransparentWindow Main = null;
	public static Camera Camera = null;	//Used instead of Camera.main

	[Tooltip("What GameObject layers should trigger window focus when the mouse passes over objects?")] //
	[SerializeField] LayerMask clickLayerMask = ~0;

	[Tooltip("Allows Input to be detected even when focus is lost")] //
	[SerializeField] bool useSystemInput = false;

	[Tooltip("Should the window be fullscreen?")] //
	[SerializeField] bool fullscreen = true;

	[Tooltip("Force the window to match ScreenResolution")] //
	[SerializeField] bool customResolution = true;

	[Tooltip("Resolution the overlay should run at")] //
	[SerializeField] Vector2Int screenResolution = new Vector2Int(1280, 720);

	[Tooltip("The framerate the overlay should try to run at")] //
	[SerializeField] int targetFrameRate = 30;

	
	/////////////////////
	//Windows DLL stuff//
	/////////////////////
	
	[DllImport("user32.dll")]
	static extern IntPtr GetActiveWindow();
	
	///<a href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlonga"> SetWIndowLongA Documentation </a>
	[DllImport("user32.dll")]
	static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

	[DllImport("user32.dll", EntryPoint = "SetLayeredWindowAttributes")]
	static extern int SetLayeredWindowAttributes(IntPtr hwnd, int crKey, byte bAlpha, int dwFlags);

	[DllImport("user32.dll", EntryPoint = "GetWindowRect")]
	static extern bool GetWindowRect(IntPtr hwnd, out Rectangle rect);
	
	[DllImport("user32.dll")]
	static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

	[DllImportAttribute("user32.dll")]
	static extern bool ReleaseCapture();

	///<a href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos"> SetWindowPos Documentation</a> 
	[DllImport("user32.dll", EntryPoint = "SetWindowPos")]
	static extern int SetWindowPos(IntPtr hwnd, int hwndInsertAfter, int x, int y, int cx, int cy, int uFlags);

	[DllImport("Dwmapi.dll")]
	static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref Rectangle margins);

	const int GWL_STYLE = -16;
	const int GWL_EXSTYLE=-20;
	const uint WS_POPUP = 0x80000000;
	const uint WS_VISIBLE = 0x10000000;
	const uint WS_EX_TRANSPARENT = 0x00000020;
	const uint WS_EX_LAYERED=0x00080000;
	const int HWND_TOPMOST = -1;

	const int WM_SYSCOMMAND = 0x112;
	const int WM_MOUSE_MOVE = 0xF012;

	const uint SWP_DRAWFRAME=0x0020;
	const uint SWP_SHOWWINDOW=0x0040;
	

	int fWidth;
	int fHeight;
	IntPtr hwnd = IntPtr.Zero;
	Rectangle margins;
	Rectangle windowRect;

	//BUG: Sometimes fails to SetResolution if not focused on startup - if using Start(), WindowBoundsCollider2D sometimes fails to set the correct size
	void Awake()
	{
		Main = this;

		Camera = GetComponent<Camera>();
		Camera.backgroundColor = new Color();
		Camera.clearFlags = CameraClearFlags.SolidColor;

		if (fullscreen && !customResolution)
		{
			screenResolution = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
		}
		
		Screen.SetResolution(screenResolution.x, screenResolution.y, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);

		Application.targetFrameRate = targetFrameRate;
		Application.runInBackground = true;

#if !UNITY_EDITOR
		fWidth = screenResolution.x;
		fHeight = screenResolution.y;
		margins = new Rectangle() {Left = -1};
		hwnd = GetActiveWindow();

		if (!GetWindowRect(hwnd, out windowRect))
		{
			Debug.LogError("Couldn't get Window Rect");
		}

		SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
		SetWindowPos(hwnd, HWND_TOPMOST, windowRect.Left, windowRect.Top, fWidth, fHeight, SWP_DRAWFRAME | SWP_SHOWWINDOW);
		DwmExtendFrameIntoClientArea(hwnd, ref margins);
#endif
	}

	void Update()
	{
		if (useSystemInput)
		{
			SystemInput.Process();
		}

		SetClickThrough();
	}

	//Returns true if the cursor is over a UI element or 2D physics object
	bool FocusForInput()
	{
		EventSystem eventSystem = EventSystem.current;
		if (eventSystem && eventSystem.IsPointerOverGameObject())
		{
			return true;
		}

		Vector2 pos = Camera.ScreenToWorldPoint(Input.mousePosition);
		return Physics2D.OverlapPoint(pos, clickLayerMask);
	}

	void SetClickThrough()
	{
		var focusWindow = FocusForInput();

		//Get window position
		GetWindowRect(hwnd, out windowRect);

#if !UNITY_EDITOR
		if (focusWindow)
		{
			SetWindowLong (hwnd, GWL_EXSTYLE, ~(WS_EX_LAYERED | WS_EX_TRANSPARENT));
			SetWindowPos(hwnd, HWND_TOPMOST, windowRect.Left, windowRect.Top, fWidth, fHeight, SWP_DRAWFRAME | SWP_SHOWWINDOW);
		}
		else
		{
			SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
			SetWindowLong (hwnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TRANSPARENT);
			SetLayeredWindowAttributes (hwnd, 0, 255, 2);
			SetWindowPos(hwnd, HWND_TOPMOST, windowRect.Left, windowRect.Top, fWidth, fHeight, SWP_DRAWFRAME | SWP_SHOWWINDOW);
		}
#endif
	}

	public static void DragWindow()
	{
#if !UNITY_EDITOR
		if (Screen.fullScreenMode != FullScreenMode.Windowed)
		{
			return;
		}
		ReleaseCapture ();
		SendMessage(Main.hwnd, WM_SYSCOMMAND, WM_MOUSE_MOVE, 0);
		Input.ResetInputAxes();
#endif		
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Rectangle
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}
}