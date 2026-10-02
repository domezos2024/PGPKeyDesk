Imports System.IO
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json
Imports PGPKeyDesk.Models

Namespace Services
    Public Class ProfileStore
        Private Shared ReadOnly AppData As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)

        ' Bis Version 1.0 hiess die Anwendung "OpenGPG"; vorhandene Profile werden uebernommen (2. Pfad).
        Public Shared ReadOnly [Default] As New ProfileStore(
            Path.Combine(AppData, "PGPKeyDesk", "profiles.json"),
            Path.Combine(AppData, "OpenGPG", "profiles.json"),
            New DpapiProtector())

        Private ReadOnly _storePath As String
        Private ReadOnly _legacyPath As String
        Private ReadOnly _protector As ISecretProtector

        Public Sub New(storePath As String, legacyPath As String, protector As ISecretProtector)
            _storePath = storePath
            _legacyPath = legacyPath
            _protector = protector
        End Sub

        ''' <summary>Set when the last load could not read the store; the unreadable file was moved aside, not deleted.</summary>
        Public Property LastLoadWarning As String

        Public Shared Function Load() As List(Of PGPProfile)
            Return [Default].LoadProfiles()
        End Function

        Public Shared Sub Save(profiles As List(Of PGPProfile))
            [Default].SaveProfiles(profiles)
        End Sub

        Public Function LoadProfiles() As List(Of PGPProfile)
            LastLoadWarning = Nothing

            If Not File.Exists(_storePath) Then
                If String.IsNullOrEmpty(_legacyPath) OrElse Not File.Exists(_legacyPath) Then Return New List(Of PGPProfile)
                Directory.CreateDirectory(Path.GetDirectoryName(_storePath))
                File.Copy(_legacyPath, _storePath)
            End If

            Try
                Dim fileBytes = File.ReadAllBytes(_storePath)
                Dim wasPlaintext As Boolean = False
                Dim json As String

                Try
                    json = Encoding.UTF8.GetString(_protector.Unprotect(fileBytes))
                Catch ex As CryptographicException
                    ' Unencrypted file from an older version: accepted once, re-saved encrypted below.
                    json = Encoding.UTF8.GetString(fileBytes).TrimStart(ChrW(&HFEFF))
                    wasPlaintext = True
                End Try

                Dim result = If(JsonSerializer.Deserialize(Of List(Of PGPProfile))(json), New List(Of PGPProfile))
                If wasPlaintext AndAlso result.Count > 0 Then SaveProfiles(result)
                Return result
            Catch ex As PlatformNotSupportedException
                Throw
            Catch ex As Exception
                ' Never silently continue with an empty list: the next Save would overwrite the user's keys.
                LastLoadWarning = "The profile file could not be read (" & ex.GetType().Name & "). " &
                                  "It was kept as: " & MoveAside()
                Return New List(Of PGPProfile)
            End Try
        End Function

        Public Sub SaveProfiles(profiles As List(Of PGPProfile))
            Dim dir As String = Path.GetDirectoryName(_storePath)
            If Not Directory.Exists(dir) Then Directory.CreateDirectory(dir)

            Dim options As New JsonSerializerOptions With {.WriteIndented = True}
            Dim json As String = JsonSerializer.Serialize(profiles, options)
            Dim encrypted = _protector.Protect(Encoding.UTF8.GetBytes(json))

            ' Atomic write: a crash mid-write must not destroy the existing store.
            Dim tmp = _storePath & ".tmp"
            File.WriteAllBytes(tmp, encrypted)
            File.Move(tmp, _storePath, overwrite:=True)
        End Sub

        Private Function MoveAside() As String
            Dim target = _storePath & ".unreadable-" & DateTime.UtcNow.ToString("yyyyMMddHHmmss")
            Try
                File.Move(_storePath, target)
                Return target
            Catch
                Return _storePath
            End Try
        End Function
    End Class
End Namespace
