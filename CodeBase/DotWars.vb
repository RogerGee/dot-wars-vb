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
    Private framerate As Integer = 16

    Sub Main()
        Dim teams As Integer()
        Dim startupForm As New StartForm

        If startupForm.ShowDialog() = DialogResult.Cancel Then
            End
        End If
        teams = startupForm.DotWarsSelections
        startupForm = Nothing

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

        ' Initialize the game
        GameInit(teams(0))

        Testing(teams)

        ' Run the game loop
        framerate = 50 ' for testing (and battery performance until I find something better)
        MessagePump.Run(frame, AddressOf GameLoop)

        ' Cleanup operations
        Direct3D_Cleanup()
    End Sub

    Sub Testing(ByVal teams As Integer())
        Dim testA As New DotSquad(GetType(DotFodderInfo), GetType(DotMeleeWeaponInfo), 100, teams(0))
        Dim testB As New DotSquad(GetType(DotInfantryInfo), GetType(DotDashWeaponInfo), 100, teams(0))
        Dim testC As New DotSquad(GetType(DotInfantryInfo), GetType(DotMeleeWeaponInfo), 225, teams(0))
        Dim enemyTestA As New DotSquad(GetType(DotInfantryInfo), GetType(DotMeleeWeaponInfo), 225, teams(1))
        enemyTestA.SendTo(New DotLocation(500, 500))

        testA.Arm(Of DotMeleeWeapon)()
        testB.Arm(Of DotDashWeapon)()
    End Sub

    Sub CheckWorldMotion()
        Dim w = (GetAsyncKeyState(&H57) And 32768) <> 0
        Dim a = (GetAsyncKeyState(&H41) And 32768) <> 0
        Dim s = (GetAsyncKeyState(&H53) And 32768) <> 0
        Dim d = (GetAsyncKeyState(&H44) And 32768) <> 0

        ' we want 10 pixels at 60 fps
        Dim mag As Integer
        mag = CInt(Math.Round(10.0 / 16.0 * framerate))

        If w Then
            DotLocation.WorldUp(mag)
        End If
        If a Then
            DotLocation.WorldLeft(mag)
        End If
        If s Then
            DotLocation.WorldDown(mag)
        End If
        If d Then
            DotLocation.WorldRight(mag)
        End If
    End Sub

    Private Sub Direct3D_Init()
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
    End Sub

    Private Sub Direct3D_Cleanup()
        GameObject.EndGameObjects()
        renderTarget.Dispose()
        swapChain.Dispose()
        directDevice.Dispose()
    End Sub

    Private Sub GameInit(ByVal playerTeam As Integer)
        GameObject.BeginGameObjects(renderTarget, playerTeam)

    End Sub

    Private Sub GameLoop()
        renderTarget.BeginDraw()
        renderTarget.Transform = Matrix3x2.Identity
        renderTarget.Clear(New Color4(Color.White))

        ' check for world location updates
        CheckWorldMotion()

        GameObject.RenderGameObjects(renderTarget)

        renderTarget.EndDraw()
        swapChain.Present(1, PresentFlags.None)

        Thread.Sleep(framerate)
    End Sub

    Private Sub frame_Click(ByVal sender As Object, ByVal args As MouseEventArgs)
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
