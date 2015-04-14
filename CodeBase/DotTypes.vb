Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Math

' Represents a position in 2-space and the global world coordinates for the viewing area
Structure DotLocation
    Shared wx As Integer = 0
    Shared wy As Integer = 0

    Dim px, py As Integer

    Sub New(ByVal x As Integer, ByVal y As Integer)
        px = x
        py = y
    End Sub

    Function GetRelativeLocation() As DotLocation
        Return New DotLocation(px - wx, py - wy)
    End Function

    Function ToPoint() As Point
        Return New Point(px, py)
    End Function
End Structure

' Represents a vector in 2-space
Structure DotVector
    Dim cx, cy As Integer

    Sub New(ByVal x As Integer, ByVal y As Integer)
        Me.cx = x
        Me.cy = y
    End Sub

    Sub New(ByVal Direction As Double, ByVal Magnitude As Double)
        Me.cx = CInt(Round(Magnitude * Cos(Direction)))
        Me.cy = CInt(Round(Magnitude * Sin(Direction)))
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
    Click1_alt
    Click2
    Click2_alt
    Click3
    Click3_alt
End Enum