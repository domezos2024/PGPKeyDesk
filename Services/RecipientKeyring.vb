Imports System.IO
Imports System.Text.Json
Imports PGPKeyDesk.Models

Namespace Services
    ''' <summary>
    ''' Keyring of recipient public keys (%AppData%\PGPKeyDesk\keyring.json). Public keys are not secret,
    ''' so the file is plain JSON; it is written atomically.
    ''' </summary>
    Public Class RecipientKeyring
        Public Shared ReadOnly [Default] As New RecipientKeyring(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PGPKeyDesk", "keyring.json"))

        Private ReadOnly _path As String
        Private _keys As List(Of RecipientKey)

        Public Sub New(path As String)
            _path = path
        End Sub

        Public Property LastLoadWarning As String

        Public ReadOnly Property Keys As IReadOnlyList(Of RecipientKey)
            Get
                EnsureLoaded()
                Return _keys
            End Get
        End Property

        ''' <summary>Validates and adds a key; an already known fingerprint updates that entry (no duplicates).</summary>
        Public Function AddOrUpdate(name As String, publicKeyText As String) As RecipientKey
            EnsureLoaded()
            Dim v = KeyValidator.ValidatePublicKey(publicKeyText)
            If Not v.IsValid Then Throw New ArgumentException(v.ErrorMessage, NameOf(publicKeyText))
            If v.IsRevoked Then Throw New ArgumentException("This key has been revoked.", NameOf(publicKeyText))
            If Not v.HasEncryptionKey Then Throw New ArgumentException("This key contains no encryption key.", NameOf(publicKeyText))

            Dim displayName = If(String.IsNullOrWhiteSpace(name), If(String.IsNullOrEmpty(v.UserId), v.Fingerprint, v.UserId), name.Trim())
            Dim existing = FindByFingerprint(v.Fingerprint)
            If existing IsNot Nothing Then
                existing.Name = displayName
                existing.PublicKey = publicKeyText.Trim()
            Else
                existing = New RecipientKey With {
                    .Name = displayName, .Fingerprint = v.Fingerprint, .PublicKey = publicKeyText.Trim()}
                _keys.Add(existing)
            End If
            Persist()
            Return existing
        End Function

        Public Function Remove(id As String) As Boolean
            EnsureLoaded()
            Dim n = _keys.RemoveAll(Function(k) k.Id = id)
            If n > 0 Then Persist()
            Return n > 0
        End Function

        Public Function FindByFingerprint(fingerprint As String) As RecipientKey
            EnsureLoaded()
            Return _keys.FirstOrDefault(Function(k) String.Equals(k.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
        End Function

        Public Sub Reload()
            _keys = Nothing
        End Sub

        Private Sub EnsureLoaded()
            If _keys IsNot Nothing Then Return
            _keys = New List(Of RecipientKey)
            LastLoadWarning = Nothing
            If Not File.Exists(_path) Then Return
            Try
                _keys = If(JsonSerializer.Deserialize(Of List(Of RecipientKey))(File.ReadAllText(_path)), New List(Of RecipientKey))
            Catch ex As Exception
                Dim aside = _path & ".unreadable-" & DateTime.UtcNow.ToString("yyyyMMddHHmmss")
                Try
                    File.Move(_path, aside)
                Catch
                    aside = _path
                End Try
                LastLoadWarning = "The keyring could not be read (" & ex.GetType().Name & "). It was kept as: " & aside
                _keys = New List(Of RecipientKey)
            End Try
        End Sub

        Private Sub Persist()
            Dim dir = Path.GetDirectoryName(_path)
            If Not Directory.Exists(dir) Then Directory.CreateDirectory(dir)
            Dim tmp = _path & ".tmp"
            File.WriteAllText(tmp, JsonSerializer.Serialize(_keys, New JsonSerializerOptions With {.WriteIndented = True}))
            File.Move(tmp, _path, overwrite:=True)
        End Sub
    End Class
End Namespace
