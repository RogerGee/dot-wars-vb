Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Math

' Represents a bounding rectangle and encapsuates common operations
Structure DotRectangle
    Dim location As DotLocation
    Dim width, height As Integer

    Shared ReadOnly Property WorldViewRectangle As DotRectangle
        Get
            Return New DotRectangle(DotLocation.wx, DotLocation.wy, DotWars.FRAME_WIDTH, DotWars.FRAME_HEIGHT)
        End Get
    End Property

    Sub New(ByVal px As Integer, ByVal py As Integer, ByVal pwidth As Integer, ByVal pheight As Integer)
        location = New DotLocation(px, py)
        width = pwidth
        height = pheight
    End Sub

    Sub New(ByVal location As DotLocation, ByVal size As Size)
        x = location.px
        y = location.py
        width = size.Width
        height = size.Height
    End Sub

    Property x As Integer
        Get
            Return location.px
        End Get
        Set(value As Integer)
            location.px = value
        End Set
    End Property

    Property y As Integer
        Get
            Return location.py
        End Get
        Set(value As Integer)
            location.py = value
        End Set
    End Property

    ReadOnly Property size As Size
        Get
            Return New Size(width, height)
        End Get
    End Property

    Function TopLeftCorner() As DotLocation
        TopLeftCorner = New DotLocation(x, y)
    End Function
    Function TopRightCorner() As DotLocation
        TopRightCorner = New DotLocation(x + width, y)
    End Function
    Function BotLeftCorner() As DotLocation
        BotLeftCorner = New DotLocation(x, y + height)
    End Function
    Function BotRightCorner() As DotLocation
        BotRightCorner = New DotLocation(x + width, y + height)
    End Function

    Function Overlaps(ByVal rect As DotRectangle) As Boolean
        Overlaps = x <= rect.x + rect.width AndAlso rect.x <= x + width _
            AndAlso y <= rect.y + rect.height AndAlso rect.y <= y + height
    End Function
End Structure

' Represents a position in 2-space and the global world coordinates for the viewing area
Structure DotLocation
    ' world coordinates - these correspond to the global world position
    ' for the upper-left hand corner of the window screen
    Shared wx As Integer = 0
    Shared wy As Integer = 0

    Shared ReadOnly InvalidLocation As New DotLocation(-1, -1)

    Dim px, py As Integer

    Sub New(ByVal x As Integer, ByVal y As Integer)
        px = x
        py = y
    End Sub

    Sub New(ByVal pnt As Point, Optional ByVal ApplyWorldViewOffset As Boolean = False)
        If ApplyWorldViewOffset Then
            px = pnt.X + wx
            py = pnt.Y + wy
        Else
            px = pnt.X
            py = pnt.Y
        End If
    End Sub

    Function GetRelativeLocation() As DotLocation
        Return New DotLocation(px - wx, py - wy)
    End Function

    Function ToPoint() As Point
        Return New Point(px, py)
    End Function

    Function IsWithin(ByVal rect As DotRectangle) As Boolean
        IsWithin = px >= rect.x AndAlso px <= rect.x + rect.width _
            AndAlso py >= rect.y AndAlso py <= rect.y + rect.height
    End Function

    Shared Operator +(ByVal left As DotLocation, ByVal right As DotLocation) As DotLocation
        Return New DotLocation(left.px + right.px, left.py + right.py)
    End Operator

    Shared Operator -(ByVal left As DotLocation, ByVal right As DotLocation) As DotLocation
        Return New DotLocation(left.px - right.px, left.py - right.py)
    End Operator
End Structure

' Represents a vector in 2-space
Structure DotVector
    Dim cx, cy As Double

    Sub New(ByVal x As Double, ByVal y As Double)
        Me.cx = x
        Me.cy = y
    End Sub

    Sub New(ByVal Direction As Double, ByVal Magnitude As Double, ByVal dummy As Boolean)
        Me.cx = Magnitude * Cos(Direction)
        Me.cy = Magnitude * Sin(Direction)
    End Sub

    Shared Operator +(ByVal left As DotVector, ByVal right As DotVector) As DotVector
        Return New DotVector(left.cx + right.cx, left.cy + right.cy)
    End Operator

    Function GetDirection() As Double
        GetDirection = Atan2(cy, cx)
    End Function

    Function GetMagnitude() As Double
        ' get the distance between the origin and the point (cx, cy)
        GetMagnitude = Sqrt(cx * cx + cy * cy)
    End Function
End Structure

Enum DotClickKind
    Click1
    Click1_shift
    Click1_ctrl
    Click1_alt
    Click2
    Click2_shift
    Click2_ctrl
    Click3
    Click3_shift
    Click3_ctrl
End Enum