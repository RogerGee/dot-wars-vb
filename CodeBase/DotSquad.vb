Option Strict On
Option Explicit On

Imports System

' Represents a collection of DotUnit objects
Class DotSquad
    Shared WithEvents hook As New DummyObject
    Shared selcounter As Integer = 0
    Private dots() As DotUnit
    Private sel As Boolean = False
    Private selpos As Integer = 0
    Private formed As Boolean = False

    Sub New(ByVal unitInfo As Type, ByVal slots As Integer, ByVal team As Integer)
        Dim info As DotUnitInfo = CType(Activator.CreateInstance(unitInfo, team), DotUnitInfo)
        ReDim dots(slots - 1)

        For i = 0 To slots - 1
            dots(i) = New DotUnit(info)

            AddHandler dots(i).OnClick, AddressOf OnClick
        Next

        AddHandler hook.OnExternClick, AddressOf OnExternClick
        AddHandler hook.OnUpdateHook, AddressOf OnUpdate
    End Sub

    Function IsDead() As Boolean
        IsDead = True

        For Each dot In dots
            If Not dot Is Nothing AndAlso Not dot.IsDead() Then
                IsDead = False
                Exit Function
            End If
        Next
    End Function

    Sub SendTo(ByVal location As DotLocation)
        Dim n As Integer = 0
        Dim i As Integer
        Dim cols As Integer
        Dim ddds() As DotUnit
        Dim l As DotLocation

        ' count dots in the squad that are alive
        ReDim ddds(dots.Length)
        For Each dot In dots
            If Not dot Is Nothing AndAlso Not dot.IsDead() Then
                ddds(n) = dot
                n += 1
            End If
        Next

        If n = 0 Then Exit Sub

        ' find dimensions for the squad that are as square as possible
        cols = CInt(Math.Sqrt(n))

        ' swap randomly selected elements to make the dots reorder their positions (for effect)
        For i = 1 To n
            Dim t As DotUnit
            Dim j, k As Integer
            j = CInt(Math.Floor(n * Rnd()))
            k = CInt(Math.Floor(n * Rnd()))
            t = ddds(j)
            ddds(j) = ddds(k)
            ddds(k) = t
        Next

        ' send the individual dots to their locations
        i = 0
        l = location
        Do
            ddds(i).SendTo(l)
            l.px += ddds(0).Width

            i += 1
            If i Mod cols = 0 Then
                l.px = location.px
                l.py += ddds(0).Height
            End If
        Loop Until i = n

        ' the squad is considered formed at this point
        formed = True
    End Sub

    Sub StackUp(ByVal location As DotLocation)
        For Each dot In dots
            If Not dot Is Nothing AndAlso Not dot.IsDead() Then
                dot.SendTo(location)
            End If
        Next
        formed = False ' not formed, they're stacked
    End Sub

    Private Sub OnClick(ByVal gobj As GameObject, ByVal kind As DotClickKind, ByVal location As DotLocation)
        If kind = DotClickKind.Click1 OrElse kind = DotClickKind.Click1_ctrl Then
            ' only do anything if the squad is not selected; if another squad was selected (counter>0), then don't select unless
            ' the control key was down (this allows the user to select multiple squads)
            If Not sel AndAlso ((kind = DotClickKind.Click1 AndAlso selcounter = 0) OrElse kind = DotClickKind.Click1_ctrl) Then
                SelectSquad()
                If Not formed Then SendTo(gobj.Location)
            End If
        ElseIf kind = DotClickKind.Click1_alt Then
            ' allow the user to cycle through overlapping squads by position, but
            ' allow the squad to be selected initially
            If selpos = 0 AndAlso selcounter = 0 Then
                If Not sel Then SelectSquad()
            ElseIf selpos > 0 Then
                DeselectSquad()
            End If
        ElseIf kind = DotClickKind.Click1_shift Then
            ' if shift is held then stack up (only the top level squad should be stacked)
            If selpos = selcounter Then
                StackUp(gobj.Location)
            End If
        End If
    End Sub

    Private Sub OnExternClick(ByVal gobj As GameObject, ByVal kind As DotClickKind, ByVal location As DotLocation)
        ' if the squad is selected and the user specified a location, then send the squad there
        If sel Then
            If kind = DotClickKind.Click2 Then
                SendTo(location)
                Exit Sub
            ElseIf kind = DotClickKind.Click2_shift Then
                StackUp(location)
                Exit Sub
            End If
        End If

        ' verify that the click did not occur on a dot in the squad
        For Each dot In dots
            If Not dot Is Nothing AndAlso Not dot.IsDead() Then
                If location.IsWithin(dot.BoundingRectangle) Then
                    Exit Sub
                End If
            End If
        Next

        If sel AndAlso kind = DotClickKind.Click1 Then
            DeselectSquad()
        End If
    End Sub

    Private Sub OnUpdate(ByVal sender As Object, ByVal e As EventArgs)
        CheckDeath()
    End Sub

    Private Sub SelectSquad()
        For Each dot In dots
            If Not dot Is Nothing Then
                dot.Touch()
            End If
        Next
        sel = True
        selcounter += 1
        selpos = selcounter
    End Sub

    Private Sub DeselectSquad()
        For Each dot In dots
            If Not dot Is Nothing Then
                dot.Ignore()
            End If
        Next
        sel = False
        selcounter -= 1
        selpos = 0
    End Sub

    Private Sub CheckDeath()
        For i = 0 To dots.Length - 1
            If Not dots(i) Is Nothing AndAlso dots(i).IsDead() Then
                dots(i) = Nothing
            End If
        Next
    End Sub
End Class
