Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Threading
Imports System.Windows.Forms
Imports SlimDX
Imports SlimDX.DXGI
Imports SlimDX.Direct2D
Imports SlimDX.Direct3D11
Imports SlimDX.Windows
Imports Device = SlimDX.Direct3D11.Device
Imports Resource = SlimDX.Direct3D11.Resource
Imports Factory = SlimDX.Direct2D.Factory

Module DotWars
    ' declares
    Public Declare Function GetAsyncKeyState Lib "User32.dll" (ByVal key As Integer) As Short

    ' constants
    Const APP_VERSION = "0.1"
    Public Const FRAME_WIDTH = 800
    Public Const FRAME_HEIGHT = 600

    ' private globals
    Private frame As RenderForm
    Private directDevice As Device
    Private renderTarget As RenderTarget
    Private swapChain As SwapChain

    ' testing
    Dim testA As New DotSquad(GetType(DotFodderInfo), 100, 1)
    Dim testB As New DotSquad(GetType(DotInfantryInfo), 81, 1)
    Dim testC As New DotSquad(GetType(DotFodderInfo), 225, 1)

    Sub Main()
        Randomize()

        frame = New RenderForm("DotWars - v" + APP_VERSION)
        With frame
            .ClientSize = New Size(FRAME_WIDTH, FRAME_HEIGHT)
            .FormBorderStyle = FormBorderStyle.FixedSingle
            .MaximizeBox = False
            .Icon = Nothing
        End With

        ' add event handlers for main frame
        AddHandler frame.MouseClick, AddressOf frame_Click

        ' Initialize Direct3D
        Direct3D_Init()

        ' Run the game loop
        MessagePump.Run(frame, AddressOf GameLoop)

        ' Cleanup operations
        Direct3D_Cleanup()
    End Sub

    Sub Direct3D_Init()
        ''' Initialize Direct 3D 11 (this mostly follows the SlimDX tutorial online)

        Dim desc As New SwapChainDescription
        With desc
            .BufferCount = 2
            .Usage = Usage.RenderTargetOutput
            .OutputHandle = frame.Handle
            .IsWindowed = True
            .ModeDescription = New ModeDescription(0, 0, New Rational(60, 1), Format.R8G8B8A8_UNorm)
            .SampleDescription = New SampleDescription(1, 0)
            .Flags = SwapChainFlags.AllowModeSwitch
            .SwapEffect = SwapEffect.Discard
        End With

        Device.CreateWithSwapChain(DriverType.Warp, DeviceCreationFlags.BgraSupport, desc, directDevice, swapChain)

        Dim buffer = Surface.FromSwapChain(swapChain, 0)

        Using factory As New Direct2D.Factory
            Dim props As New RenderTargetProperties

            With props
                .HorizontalDpi = factory.DesktopDpi.Width
                .VerticalDpi = factory.DesktopDpi.Height
                .MinimumFeatureLevel = Direct2D.FeatureLevel.Default
                .PixelFormat = New PixelFormat(Format.Unknown, AlphaMode.Ignore)
                .Type = RenderTargetType.Default
                .Usage = RenderTargetUsage.None
            End With

            renderTarget = renderTarget.FromDXGI(factory, buffer, props)
        End Using

        ' disallow Alt-Enter keystroke for full screen
        Using factory = swapChain.GetParent(Of DXGI.Factory)()
            factory.SetWindowAssociation(frame.Handle, WindowAssociationFlags.IgnoreAltEnter)
        End Using

        ' init other modules
        GameObject.BeginGameObjects(renderTarget)
    End Sub

    Sub Direct3D_Cleanup()
        GameObject.EndGameObjects()
        renderTarget.Dispose()
        swapChain.Dispose()
        directDevice.Dispose()
    End Sub

    Sub GameLoop()
        renderTarget.BeginDraw()
        renderTarget.Transform = Matrix3x2.Identity
        renderTarget.Clear(New Color4(Color.White))

        GameObject.RenderGameObjects(renderTarget)

        renderTarget.EndDraw()
        swapChain.Present(1, PresentFlags.None)

        Thread.Sleep(16)
    End Sub

    Sub frame_Click(ByVal sender As Object, ByVal args As MouseEventArgs)
        Dim kind As DotClickKind
        Dim shift = (GetAsyncKeyState(&H10) And 32768) <> 0
        Dim ctrl = (GetAsyncKeyState(&H11) And 32768) <> 0
        Dim alt = (GetAsyncKeyState(&H12) And 32768) <> 0

        ' get click kind
        Select Case args.Button
            Case MouseButtons.Left
                If shift Then
                    kind = DotClickKind.Click1_shift
                ElseIf ctrl Then
                    kind = DotClickKind.Click1_ctrl
                ElseIf alt Then
                    kind = DotClickKind.Click1_alt
                Else
                    kind = DotClickKind.Click1
                End If
            Case MouseButtons.Right
                If shift Then
                    kind = DotClickKind.Click2_shift
                ElseIf ctrl Then
                    kind = DotClickKind.Click2_ctrl
                Else
                    kind = DotClickKind.Click2
                End If
            Case Else
                If shift Then
                    kind = DotClickKind.Click3_shift
                ElseIf ctrl Then
                    kind = DotClickKind.Click3_ctrl
                Else
                    kind = DotClickKind.Click3
                End If
        End Select

        GameObject.ClickGameObjects(kind, New DotLocation(args.Location, True))
    End Sub
End Module
