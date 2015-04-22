Option Strict On
Option Explicit On

Imports System

' Represents a collection of DotUnit objects that share a common weapon
' type, weapon info and unit info
Class DotSquad
    Shared WithEvents hook As New DummyObject
    Shared selcounter As Integer = 0
    Private uinfo As DotUnitInfo
    Private winfo As DotWeaponInfo
    Private dots() As DotUnit
    Private sel As Boolean = False
    Private selpos As Integer = 0
    Private formed As Boolean = False
    Private heading As DotLocation = DotLocation.InvalidLocation

    Sub New(ByVal unitInfo As Type, ByVal weaponInfo As Type, ByVal slots As Integer, ByVal team As Integer)
        uinfo = CType(Activator.CreateInstance(unitInfo, team), DotUnitInfo)
        winfo = CType(Activator.CreateInstance(weaponInfo), DotWeaponInfo)
        ReDim dots(slots - 1)

        For i = 0 To slots - 1
            ' create a dot and give it a weapon
            dots(i) = New DotUnit(uinfo)

            AddHandler dots(i).OnClick, AddressOf OnClick
            AddHandler dots(i).OnTargeted, AddressOf OnTargeted
        Next

        AddHandler hook.OnExternClick, AddressOf OnExternClick
        AddHandler hook.OnUpdateHook, AddressOf OnUpdate
    End Sub

    ReadOnly Property Location As DotLocation
        Get
            For Each dot In dots
                If dot IsNot Nothing AndAlso Not dot.IsDead() Then Return dot.Location
                Return DotLocation.InvalidLocation
            Next
        End Get
    End Property

    ReadOnly Property Units As DotUnit()
        Get
            Dim result() As DotUnit
            Dim army As New System.Collections.ObjectModel.Collection(Of DotUnit)
            For Each dot In dots
                If dot IsNot Nothing AndAlso Not dot.IsDead() Then army.Add(dot)
            Next
            ReDim result(0 To army.Count - 1)
            army.CopyTo(result, 0)
            Return result
        End Get
    End Property

    Sub Arm(Of T As DotWeapon)()
        Dim wt = GetType(T)
        For Each dot In dots
            If dot IsNot Nothing Then
                dot.Arm(wt, winfo)
            End If
        Next
    End Sub

    Sub Attack(ByVal obj As GameObject)
        For Each dot In dots
            If dot IsNot Nothing AndAlso Not dot.IsDead() Then
                dot.AttackObject(obj)
            End If
        Next
    End Sub

    Sub AttackSquad(ByVal other As DotSquad)
        Dim i As Integer
        Dim enemies = other.Units

        ' assign a target to each dot in the squad; cycle around and start re-assigning
        ' if we outnumber the enemy
        i = 0
        For Each dot In dots
            If dot IsNot Nothing AndAlso Not dot.IsDead() Then dot.AttackObject(enemies(i Mod enemies.Length))
            i += 1
        Next
    End Sub

    Sub CeaseFire()
        For Each dot In dots
            If dot IsNot Nothing AndAlso Not dot.IsDead() Then
                dot.CeaseFire()
            End If
        Next
    End Sub

    Function IsDead() As Boolean
        IsDead = True

        For Each dot In dots
            If dot IsNot Nothing AndAlso Not dot.IsDead() Then
                IsDead = False
                Exit Function
            End If
        Next
    End Function

    Sub SendTo(ByVal location As DotLocation, Optional ByVal scatter As Boolean = True)
        Dim n As Integer = 0
        Dim i As Integer
        Dim cols As Integer
        Dim ddds() As DotUnit
        Dim l As DotLocation

        ' count dots in the squad that are alive
        ReDim ddds(dots.Length)
        For Each dot In dots
            If dot IsNot Nothing AndAlso Not dot.IsDead() Then
                ddds(n) = dot
                n += 1
            End If
        Next

        If n = 0 Then Exit Sub

        ' find dimensions for the squad that are as square as possible
        cols = CInt(Math.Sqrt(n))

        If scatter Then
            ' swap randomly selected elements (scatter) to make the dots reorder their positions (for effect)
            For i = 1 To n
                Dim t As DotUnit
                Dim j, k As Integer
                j = CInt(Math.Floor(n * Rnd()))
                k = CInt(Math.Floor(n * Rnd()))
                t = ddds(j)
                ddds(j) = ddds(k)
                ddds(k) = t
            Next
        End If

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
        heading = location
    End Sub

    Sub StackUp(ByVal location As DotLocation)
        For Each dot In dots
            If dot IsNot Nothing AndAlso Not dot.IsDead() Then
                dot.SendTo(location)
            End If
        Next
        formed = False ' not formed, they're stacked
        heading = location
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
                If gobj Is Nothing Then
                    ' if they right click and do not select an enemy, then cease fire
                    ' and send to that location
                    CeaseFire()
                    SendTo(location)
                Else
                    ' 'gobj' is the new target for the squad
                    Dim squad As DotSquad = Nothing
                    gobj.Target(squad)
                    ' attacking an object will send the dots to the 
                    ' target's location
                    If squad IsNot Nothing Then
                        ' send all dots to attack all members of the enemy squad
                        AttackSquad(squad)
                    Else
                        ' if no squad was found, send all our dots to attack
                        ' the single object
                        Attack(gobj)
                    End If
                End If
                Exit Sub
            ElseIf kind = DotClickKind.Click2_shift Then
                CeaseFire()
                StackUp(location)
                Exit Sub
            End If
        End If

        ' verify that the click did not occur on a dot in the squad
        For Each dot In dots
            If dot IsNot Nothing AndAlso Not dot.IsDead() Then
                If location.IsWithin(dot.BoundingRectangle) Then
                    Exit Sub
                End If
            End If
        Next

        ' deselect if left button went down outside and another squad wasn't selected
        ' (it will be deselected in the other routine if another squad is selected)
        If sel AndAlso kind = DotClickKind.Click1 Then
            DeselectSquad()
        End If
    End Sub

    Private Sub OnUpdate(ByVal sender As Object, ByVal e As EventArgs)
        If CheckDeath() Then
            'SendTo(If(Me.heading.Equals(DotLocation.InvalidLocation), Me.Location, Me.heading))
        End If
    End Sub

    Private Sub OnTargeted(ByVal obj As GameObject, ByRef squad As DotSquad)
        ' when a dot is targeted, get its squad so that the caller can target all the dots
        squad = Me
    End Sub

    Private Sub SelectSquad()
        For Each dot In dots
            If dot IsNot Nothing Then
                dot.Touch()
            End If
        Next
        sel = True
        selcounter += 1
        selpos = selcounter
    End Sub

    Private Sub DeselectSquad()
        For Each dot In dots
            If dot IsNot Nothing Then
                dot.Ignore()
            End If
        Next
        sel = False
        selcounter -= 1
        selpos = 0
    End Sub

    Private Function CheckDeath() As Boolean
        Dim atLeast = False
        CheckDeath = False

        For i = 0 To dots.Length - 1
            If dots(i) IsNot Nothing Then
                If dots(i).IsDead() Then
                    dots(i) = Nothing
                    CheckDeath = True
                Else
                    atLeast = True
                End If
            End If
        Next

        Return CheckDeath AndAlso atLeast
    End Function
End Class
