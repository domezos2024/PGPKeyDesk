Imports System.IO
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json
Imports PGPKeyDesk.Models

Namespace Services
    Public Class ProfileStore
        Private Shared ReadOnly StorePath As String = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PGPKeyDesk", "profiles.json")

        ' Bis Version 1.0 hiess die Anwendung "OpenGPG"; vorhandene Profile werden uebernommen.
        Private Shared ReadOnly LegacyStorePath As String = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenGPG", "profiles.json")

        Public Shared Function Load() As List(Of PGPProfile)
            Try
                If Not File.Exists(StorePath) Then
                    If Not File.Exists(LegacyStorePath) Then Return New List(Of PGPProfile)
                    Directory.CreateDirectory(Path.GetDirectoryName(StorePath))
                    File.Copy(LegacyStorePath, StorePath)
                End If

                Dim fileBytes = File.ReadAllBytes(StorePath)
                Dim json As String

                Try
                    Dim plainBytes = ProtectedData.Unprotect(fileBytes, Nothing, DataProtectionScope.CurrentUser)
                    json = Encoding.UTF8.GetString(plainBytes)
                Catch ex As CryptographicException
                    json = Encoding.UTF8.GetString(fileBytes)
                    Dim migrated = JsonSerializer.Deserialize(Of List(Of PGPProfile))(json)
                    If migrated IsNot Nothing AndAlso migrated.Count > 0 Then Save(migrated)
                    Return If(migrated, New List(Of PGPProfile))
                End Try

                Dim result = JsonSerializer.Deserialize(Of List(Of PGPProfile))(json)
                Return If(result, New List(Of PGPProfile))
            Catch
                Return New List(Of PGPProfile)
            End Try
        End Function

        Public Shared Sub Save(profiles As List(Of PGPProfile))
            Dim dir As String = Path.GetDirectoryName(StorePath)
            If Not Directory.Exists(dir) Then Directory.CreateDirectory(dir)

            Dim options As New JsonSerializerOptions With {.WriteIndented = True}
            Dim json As String = JsonSerializer.Serialize(profiles, options)
            Dim plainBytes = Encoding.UTF8.GetBytes(json)
            Dim encryptedBytes = ProtectedData.Protect(plainBytes, Nothing, DataProtectionScope.CurrentUser)
            File.WriteAllBytes(StorePath, encryptedBytes)
        End Sub
    End Class
End Namespace
