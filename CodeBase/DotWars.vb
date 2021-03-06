﻿Option Strict On
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
Imports DrawBrush = SlimDX.Direct2D.SolidColorBrush

Module DotWars
    ' declares
    Public Declare Function GetAsyncKeyState Lib "User32.dll" (ByVal key As Integer) As Short

    ' constants
    Const APP_VERSION = "0.1"
    Public FRAME_WIDTH As Integer
    Public FRAME_HEIGHT As Integer
    Const TESTING_ENABLED As Boolean = True

    ' private globals
    Private teams As Integer()
    Private frame As RenderForm
    Private directDevice As Device
    Private renderTarget As RenderTarget
    Private swapChain As SwapChain
    Private framerate As Integer = 16
    Private moveVector As New Point(0, 0)

    Sub Main()
        Dim startupForm As New StartForm

        If startupForm.ShowDialog() = DialogResult.Cancel Then
            End
        End If
        teams = startupForm.DotWarsSelections
        startupForm = Nothing

        Randomize()

        frame = New RenderForm("DotWars - v" + APP_VERSION)
        With frame
            '.ClientSize = New Size(FRAME_WIDTH, FRAME_HEIGHT)
            .WindowState = FormWindowState.Normal
            .FormBorderStyle = FormBorderStyle.None
            .Bounds = Screen.PrimaryScreen.Bounds
            .MaximizeBox = False
            .Icon = Nothing
        End With
        FRAME_WIDTH = frame.ClientSize.Width
        FRAME_HEIGHT = frame.ClientSize.Height

        ' add event handlers for main frame
        AddHandler frame.MouseClick, AddressOf frame_Click
        AddHandler frame.KeyUp, AddressOf frame_KeyUp
        AddHandler frame.MouseMove, AddressOf frame_MouseMove

        ' Initialize Direct3D
        Direct3D_Init()

        ' Initialize the game
        GameInit(teams(0))

        ' Run the game loop
        framerate = 16
        MessagePump.Run(frame, AddressOf GameLoop)

        ' Cleanup operations
        Direct3D_Cleanup()
    End Sub

    Sub CheckWorldMotion()
        Dim w = (GetAsyncKeyState(&H57) And 32768) <> 0
        Dim a = (GetAsyncKeyState(&H41) And 32768) <> 0
        Dim s = (GetAsyncKeyState(&H53) And 32768) <> 0
        Dim d = (GetAsyncKeyState(&H44) And 32768) <> 0

        ' we want 10 pixels at 60 fps: if the user is using the mouse, try make
        ' it 20 pixels (this way they can fine tune with keyboard and move around
        ' quickly with the mouse)
        Dim mag As Integer
        mag = CInt(Math.Round(If(w OrElse a OrElse s OrElse d, 10.0, 20.0) / 16.0 * framerate))

        If w OrElse moveVector.Y < 0 Then
            DotLocation.WorldUp(mag)
        End If
        If a OrElse moveVector.X < 0 Then
            DotLocation.WorldLeft(mag)
        End If
        If s OrElse moveVector.Y > 0 Then
            DotLocation.WorldDown(mag)
        End If
        If d OrElse moveVector.X > 0 Then
            DotLocation.WorldRight(mag)
        End If
    End Sub

    Sub BrushAlpha(ByVal br As DrawBrush, ByVal percent As Single, ByRef holdColor As SlimDX.Color4)
        Dim clr As SlimDX.Color4
        holdColor = br.Color
        clr = holdColor
        clr.Alpha = percent
        br.Color = clr
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

        ' DriverType should be hardware for release builds
        Device.CreateWithSwapChain(DriverType.Warp, DeviceCreationFlags.BgraSupport, desc, directDevice, swapChain)

        Dim buffer = Surface.FromSwapChain(swapChain, 0)

        Using factory As New Direct2D.Factory
            Dim props As New RenderTargetProperties

            With props
                ' the DPI must be as close as possible to screen pixels (since
                ' we use those units in the game logic)
                .HorizontalDpi = 96.0 'factory.DesktopDpi.Width
                .VerticalDpi = 96.0 ' factory.DesktopDpi.Height
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
        renderTarget.Clear(New Color4(Color.Black))

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

    Private Sub frame_KeyUp(ByVal sender As Object, ByVal e As KeyEventArgs)
        If e.KeyCode = Keys.Escape Then
            End
        ElseIf e.KeyCode = Keys.T Then
            If TESTING_ENABLED Then ' do testbed actions
                ' unit types
                Dim fodder = GetType(DotFodderInfo)
                Dim infantry = GetType(DotInfantryInfo)
                Dim calvary = GetType(DotCalvaryInfo)

                ' weapon types and weapon parameter types
                Dim dashWeapon = GetType(DotDashWeapon)
                Dim starWeapon = GetType(DotStarWeapon)
                Dim meleeWeapon = GetType(DotMeleeWeapon)
                Dim rangedInfo = GetType(DotRangedWeaponInfoC)
                Dim meleeInfo = GetType(DotMeleeWeaponInfo)
                Dim calvaryMeleeInfo = GetType(DotCalvaryMeleeWeaponInfo)

                ' generate player's stuff
                Dim info As New DotUnitGeneratorInfo(teams(0), New DotRectangle(20, 20, 100, 180), 500, starWeapon, New DotRangedWeaponInfoC)
                Dim str As DotUnitGenerator
                Dim initial = New DotSquad(infantry, 16, teams(0))
                With info
                    .UnitType = fodder
                    .WeaponType = dashWeapon
                    .WeaponInfo = rangedInfo
                    .Slots = 25
                    .GenerateTime = 5
                End With
                str = New DotUnitGenerator(info)
                initial.Arm(dashWeapon, rangedInfo)

                ' generate enemy stuff
                Dim sqds = New DotSquad() { _
                    New DotSquad(infantry, 64, teams(1)), _
                    New DotSquad(fodder, 25, teams(1)), _
                    New DotSquad(infantry, 64, teams(1)), _
                    New DotSquad(fodder, 25, teams(1)), _
                    New DotSquad(calvary, 16, teams(1)), _
                    New DotSquad(infantry, 100, teams(1))
                }

                sqds(0).Arm(starWeapon, rangedInfo)
                sqds(0).SendTo(New DotLocation(2000, 2000))
                sqds(1).Arm(meleeWeapon, meleeInfo)
                sqds(1).PortTo(New DotLocation(2000, 2400))
                sqds(2).Arm(dashWeapon, rangedInfo)
                sqds(2).SendTo(New DotLocation(1800, 1800))
                sqds(3).Arm(meleeWeapon, meleeInfo)
                sqds(3).SendTo(New DotLocation(1800, 400 + 1800))
                sqds(4).Arm(meleeWeapon, calvaryMeleeInfo)
                sqds(4).SendTo(New DotLocation(1500, 1500))
                sqds(5).SendTo(New DotLocation(200, 300))
                sqds(1).AttackSquad(initial)
            End If
        End If
    End Sub

    Private Sub frame_MouseMove(ByVal sender As Object, ByVal e As MouseEventArgs)
        If e.X = 0 Then
            moveVector.X = -1
        ElseIf e.X = FRAME_WIDTH - 1 Then
            moveVector.X = 1
        Else
            moveVector.X = 0
        End If

        If e.Y = 0 Then
            moveVector.Y = -1
        ElseIf e.Y = FRAME_HEIGHT - 1 Then
            moveVector.Y = 1
        Else
            moveVector.Y = 0
        End If
    End Sub
End Module
