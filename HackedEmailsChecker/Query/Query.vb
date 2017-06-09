﻿Public Class Query
    Public Property StopExecuteQueryFlag As Boolean = False

#Region "Property HasErrors"
    Private hasErrorsValue As Boolean = False

    Public ReadOnly Property HasErrors As Boolean
        Get
            Return Me.hasErrorsValue
        End Get
    End Property
#End Region

#Region "Property Results"
    Private resultsValue As List(Of QueryResult) = New List(Of QueryResult)
    Public ReadOnly Property Results As List(Of QueryResult)
        Get
            Return Me.resultsValue
        End Get
    End Property
#End Region

    Public Property DatabaseHaveIBeenPwnedEnabled As Boolean = True
    Private DatabaseHaveIBeenPwnedLastRequest As DateTime? = Nothing

    Public Property DatabaseHackedEmails As Boolean = True
    Private DatabaseHackedEmailsLastRequest As DateTime? = Nothing

    Public Property EnableCache As Boolean = True

    Public Const CacheTTLDefaultValue As UInt16 = 48
    Public Property CacheTTL As UInt16 = Query.CacheTTLDefaultValue

    Public Const DatabaseRequestDelayDefaulValue As UInt64 = 2000
    Public Property DatabaseRequestDelay As UInt64 = Query.DatabaseRequestDelayDefaulValue

    Public DatabaseLastRequest As New System.Collections.Generic.Dictionary(Of Util.Databases, DateTime)

#Region "Method Execute"
    Public Overloads Sub Execute(email As String)
        Dim emails As New List(Of String)
        emails.Add(email)
        Me.Execute(emails)
    End Sub
    Public Overloads Sub Execute(emails As List(Of String))
        'Clear Results
        Me.resultsValue = Nothing
        Me.resultsValue = New List(Of QueryResult)

        'Clear request info
        Me.DatabaseLastRequest.Clear()

        For Each email In emails
            For Each database As Util.Databases In System.Enum.GetValues(GetType(Util.Databases))
                If database = Util.Databases.Undefined Then Continue For

                'Check flag Stop Execute
                If Me.StopExecuteQueryFlag Then
                    Me.AddOutputInformation("The query execution has been interrupted.")
                    Exit For
                End If

                Dim result As QueryResult = Nothing

                'Search in cache
                If Me.EnableCache Then
                    Try
                        result = Util.FindInCache(database, email, Me.CacheTTL)
                        If result IsNot Nothing Then
                            Me.AddOutputInformation(String.Format("Read from cache for {0} on {1} (TTL={2:#,##0.0})... ", email, database.GetDescription(), result.QueryExecutionTTL))
                        End If
                    Catch ex As Exception
                        Me.AddOutputError("Find in cache for " & database.GetDescription(), ex)
                    End Try
                End If

                'Query
                If Not Me.EnableCache OrElse result Is Nothing Then
                    Try
                        Me.AddOutputInformation(String.Format("Query for {0} on {1}... ", email, database.GetDescription()))

                        'Wait delay request
                        If Me.DatabaseLastRequest.ContainsKey(database) Then
                            While (Now - Me.DatabaseLastRequest(database)).TotalMilliseconds() < Me.DatabaseRequestDelay
                                System.Threading.Thread.Sleep(100)

                                'Check flag Stop Execute
                                If Me.StopExecuteQueryFlag Then
                                    Me.AddOutputInformation("The query execution has been interrupted.")
                                    Exit For
                                End If
                            End While
                        End If

                        'Request to database
                        If database = Util.Databases.HaveIBeenPwned AndAlso Me.DatabaseHaveIBeenPwnedEnabled Then
                            result = UtilHaveIBeenPwned.Query(email)
                            Me.DatabaseHaveIBeenPwnedLastRequest = Now
                        ElseIf database = Util.Databases.HackedEmails AndAlso Me.DatabaseHackedEmails Then
                            result = UtilHackedEmails.Query(email)
                            Me.DatabaseHackedEmailsLastRequest = Now
                        Else
                            Throw New System.NotImplementedException("Execute method in Query class not implemented for database " & database.ToString())
                        End If

                        'Update last request date
                        If Not Me.DatabaseLastRequest.ContainsKey(database) Then
                            Me.DatabaseLastRequest.Add(database, Now)
                        Else
                            Me.DatabaseLastRequest(database) = Now
                        End If

                    Catch ex As Exception
                        Me.AddOutputError("Read response from " & database.GetDescription(), ex)
                    End Try

                    'Save result in cache
                    If Me.EnableCache Then
                        Try
                            If result IsNot Nothing Then
                                Util.SaveQueryCache(result)
                            End If
                        Catch ex As Exception
                            Me.AddOutputError("Save query result in cache", ex)
                        End Try
                    End If
                End If

                'Raise Results Events
                If result.Found Then
                    Me.AddOutputResultEmailFoundAdded(String.Format("found {0} data leaks", result.Items.Count), result)
                Else
                    Me.AddOutputResultEmailNotFound("Not found", result)
                End If

                'Add result to list
                Me.resultsValue.Add(result)
            Next
        Next
    End Sub
#End Region

#Region "Event OutputInformationAdded"
    'Event Definition
    Public Event OutputInformationAdded As System.EventHandler(Of QueryOutputInformationAddedEventArgs)

    'Sub definition for Raise the event
    Protected Overridable Sub OnOutputInformationAdded(ByVal sender As Object, ByVal e As QueryOutputInformationAddedEventArgs)
        RaiseEvent OutputInformationAdded(sender, e)
    End Sub
    Private Sub AddOutputInformation(text As String)
        Me.OnOutputInformationAdded(Me, New QueryOutputInformationAddedEventArgs(text))
    End Sub
#End Region

#Region "Event OutputResultEmailFoundAdded"
    'Event Definition
    Public Event OutputResultEmailFoundAdded As System.EventHandler(Of QueryOutputResultEmailFoundAddedEventArgs)

    'Sub definition for Raise the event
    Protected Overridable Sub OnOutputResultEmailFoundAdded(ByVal sender As Object, ByVal e As QueryOutputResultEmailFoundAddedEventArgs)
        RaiseEvent OutputResultEmailFoundAdded(sender, e)
    End Sub
    Private Sub AddOutputResultEmailFoundAdded(text As String, result As QueryResult)
        Me.OnOutputResultEmailFoundAdded(Me, New QueryOutputResultEmailFoundAddedEventArgs(text, result))
    End Sub
#End Region

#Region "Event OutputResultEmailNotFoundAdded"
    'Event Definition
    Public Event OutputResultEmailNotFoundAdded As System.EventHandler(Of QueryOutputResultEmailNotFoundAddedEventArgs)

    'Sub definition for Raise the event
    Protected Overridable Sub OnOutputResultEmailNotFoundAdded(ByVal sender As Object, ByVal e As QueryOutputResultEmailNotFoundAddedEventArgs)
        RaiseEvent OutputResultEmailNotFoundAdded(sender, e)
    End Sub
    Private Sub AddOutputResultEmailNotFound(text As String, result As QueryResult)
        Me.OnOutputResultEmailNotFoundAdded(Me, New QueryOutputResultEmailNotFoundAddedEventArgs(text, result))
    End Sub
#End Region

#Region "Event OutputErrorAdded"
    'Event Definition
    Public Event OutputErrorAdded As System.EventHandler(Of QueryOutputErrorAddedEventArgs)

    'Sub definition for Raise the event
    Protected Overridable Sub OnOutputErrorAdded(ByVal sender As Object, ByVal e As QueryOutputErrorAddedEventArgs)
        Me.hasErrorsValue = True
        RaiseEvent OutputErrorAdded(sender, e)
    End Sub
    Private Sub AddOutputError(operation As String, ex As System.Exception)
        Me.OnOutputErrorAdded(Me, New QueryOutputErrorAddedEventArgs("Error during " & operation & ":" & ex.Message, ex))
    End Sub
#End Region
End Class