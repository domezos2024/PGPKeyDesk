Imports System.IO
Imports PgpCore

Namespace Services
    Public Enum SignatureStatus
        ''' <summary>No sender key was supplied, so a signature (if any) was not checked.</summary>
        NotChecked
        NotSigned
        Valid
        Invalid
    End Enum

    Public Enum KeyCheckLevel
        Ok
        Warning
        Blocked
    End Enum

    Public Class KeyCheckResult
        Public Property Level As KeyCheckLevel = KeyCheckLevel.Ok
        Public Property Message As String = String.Empty
    End Class

    Public Class DecryptResult
        Public Property Text As String = String.Empty
        Public Property Signature As SignatureStatus = SignatureStatus.NotChecked
    End Class

    Public Class VerifyResult
        Public Property IsValid As Boolean
        Public Property Text As String = String.Empty
    End Class

    ''' <summary>UI-independent OpenPGP operations (text and files, signing and verification) on top of PgpCore.</summary>
    Public Class PgpService

        ''' <summary>PgpCore happily encrypts to expired/revoked keys, so this is checked here first.</summary>
        Public Shared Function CheckRecipientKey(publicKey As String, nowUtc As DateTime) As KeyCheckResult
            Return CheckRecipientKey(KeyValidator.ValidatePublicKey(publicKey), nowUtc)
        End Function

        ''' <summary>Same check on an already parsed key (avoids parsing the armored text twice).</summary>
        Public Shared Function CheckRecipientKey(v As KeyValidationResult, nowUtc As DateTime) As KeyCheckResult
            Dim r As New KeyCheckResult()
            If Not v.IsValid Then
                r.Level = KeyCheckLevel.Blocked
                r.Message = "Invalid public key: " & v.ErrorMessage
            ElseIf v.IsRevoked Then
                r.Level = KeyCheckLevel.Blocked
                r.Message = "This public key has been revoked."
            ElseIf Not v.HasEncryptionKey Then
                r.Level = KeyCheckLevel.Blocked
                r.Message = "This public key contains no encryption key."
            Else
                Select Case v.GetExpiryStatus(nowUtc)
                    Case KeyExpiryStatus.Expired
                        r.Level = KeyCheckLevel.Blocked
                        r.Message = "Recipient key " & KeyExpiry.Describe(v.ExpiresAtUtc, nowUtc) & "."
                    Case KeyExpiryStatus.ExpiringSoon
                        r.Level = KeyCheckLevel.Warning
                        r.Message = "Recipient key " & KeyExpiry.Describe(v.ExpiresAtUtc, nowUtc) & "."
                End Select
            End If
            Return r
        End Function

        Private Shared Sub RequireUsable(publicKey As String)
            Dim c = CheckRecipientKey(publicKey, DateTime.UtcNow)
            If c.Level = KeyCheckLevel.Blocked Then Throw New InvalidOperationException(c.Message)
        End Sub

        ' ---------------------------------------------------------------- text
        ' All operations run on the thread pool: reading/unlocking a key (S2K) and RSA work would otherwise
        ' freeze the UI before the first real await.

        Public Shared Function EncryptTextAsync(plainText As String, recipientPublicKey As String,
                                                Optional signerPrivateKey As String = Nothing,
                                                Optional signerPassphrase As String = Nothing) As Task(Of String)
            RequireUsable(recipientPublicKey)
            Return Task.Run(
                Async Function() As Task(Of String)
                    If String.IsNullOrEmpty(signerPrivateKey) Then
                        Using pgp As New PGP(New EncryptionKeys(recipientPublicKey))
                            Return Await pgp.EncryptArmoredStringAsync(plainText)
                        End Using
                    End If
                    Using pgp As New PGP(New EncryptionKeys(recipientPublicKey, signerPrivateKey, signerPassphrase))
                        Return Await pgp.EncryptArmoredStringAndSignAsync(plainText)
                    End Using
                End Function)
        End Function

        ''' <summary>Decrypts; if senderPublicKey is given, the signature is verified as well.</summary>
        Public Shared Function DecryptTextAsync(cipherText As String, privateKey As String, passphrase As String,
                                                Optional senderPublicKey As String = Nothing) As Task(Of DecryptResult)
            Return Task.Run(
                Async Function() As Task(Of DecryptResult)
                    Dim result As New DecryptResult()
                    If String.IsNullOrWhiteSpace(senderPublicKey) Then
                        Using pgp As New PGP(New EncryptionKeys(privateKey, passphrase))
                            result.Text = Await pgp.DecryptArmoredStringAsync(cipherText)
                        End Using
                        Return result
                    End If

                    ' Fast path: one pass decrypts and verifies. Any failure falls back to a plain decrypt,
                    ' so a bad signature never hides the text and a real decrypt error is still thrown.
                    Dim verified = False
                    Try
                        Using pgp As New PGP(New EncryptionKeys(senderPublicKey, privateKey, passphrase))
                            result.Text = Await pgp.DecryptArmoredStringAndVerifyAsync(cipherText)
                        End Using
                        verified = True
                    Catch ex As Exception
                        result.Signature = ClassifySignatureError(ex)
                    End Try
                    If verified Then
                        result.Signature = SignatureStatus.Valid
                    Else
                        Using pgp As New PGP(New EncryptionKeys(privateKey, passphrase))
                            result.Text = Await pgp.DecryptArmoredStringAsync(cipherText)
                        End Using
                    End If
                    Return result
                End Function)
        End Function

        Public Shared Function SignTextAsync(text As String, signerPrivateKey As String, passphrase As String) As Task(Of String)
            Return Task.Run(
                Async Function() As Task(Of String)
                    Using pgp As New PGP(New EncryptionKeys(signerPrivateKey, passphrase))
                        Return Await pgp.ClearSignArmoredStringAsync(text)
                    End Using
                End Function)
        End Function

        Public Shared Function VerifyTextAsync(clearSignedText As String, signerPublicKey As String) As Task(Of VerifyResult)
            Return Task.Run(
                Async Function() As Task(Of VerifyResult)
                    Using pgp As New PGP(New EncryptionKeys(signerPublicKey))
                        Dim ok As Boolean
                        Try
                            ok = Await pgp.VerifyClearArmoredStringAsync(clearSignedText)
                        Catch ex As Exception
                            ok = False
                        End Try
                        Return New VerifyResult With {.IsValid = ok}
                    End Using
                End Function)
        End Function

        ' ---------------------------------------------------------------- files

        ''' <summary>
        ''' Writes to a temp file next to the target and moves it into place only on success, so a failed
        ''' operation (wrong passphrase, bad key, disk full) never leaves an empty file or clobbers an existing one.
        ''' </summary>
        Private Shared Async Function WriteAtomicAsync(outputPath As String, work As Func(Of FileInfo, Task)) As Task
            Dim tmp = outputPath & "." & Guid.NewGuid().ToString("N") & ".tmp"
            Try
                Await work(New FileInfo(tmp))
                File.Move(tmp, outputPath, overwrite:=True)
            Catch
                Try
                    File.Delete(tmp)
                Catch
                End Try
                Throw
            End Try
        End Function

        Public Shared Function EncryptFileAsync(inputPath As String, outputPath As String, recipientPublicKey As String,
                                                Optional signerPrivateKey As String = Nothing,
                                                Optional signerPassphrase As String = Nothing,
                                                Optional armor As Boolean = False) As Task
            RequireUsable(recipientPublicKey)
            Return Task.Run(
                Function() WriteAtomicAsync(outputPath,
                    Async Function(tmp As FileInfo) As Task
                        Dim input As New FileInfo(inputPath)
                        If String.IsNullOrEmpty(signerPrivateKey) Then
                            Using pgp As New PGP(New EncryptionKeys(recipientPublicKey))
                                Await pgp.EncryptFileAsync(input, tmp, armor)
                            End Using
                        Else
                            Using pgp As New PGP(New EncryptionKeys(recipientPublicKey, signerPrivateKey, signerPassphrase))
                                Await pgp.EncryptFileAndSignAsync(input, tmp, armor)
                            End Using
                        End If
                    End Function))
        End Function

        ''' <summary>Decrypts a file; with senderPublicKey the signature is verified (status returned).</summary>
        Public Shared Function DecryptFileAsync(inputPath As String, outputPath As String, privateKey As String, passphrase As String,
                                                Optional senderPublicKey As String = Nothing) As Task(Of SignatureStatus)
            Return Task.Run(
                Async Function() As Task(Of SignatureStatus)
                    Dim input As New FileInfo(inputPath)
                    Dim status = SignatureStatus.NotChecked
                    Dim verify = Not String.IsNullOrWhiteSpace(senderPublicKey)

                    Await WriteAtomicAsync(outputPath,
                        Async Function(tmp As FileInfo) As Task
                            If verify Then
                                ' Fast path: one pass decrypts and verifies; any failure falls back to a plain decrypt.
                                Dim verified = False
                                Try
                                    Using pgp As New PGP(New EncryptionKeys(senderPublicKey, privateKey, passphrase))
                                        Await pgp.DecryptFileAndVerifyAsync(input, tmp)
                                    End Using
                                    verified = True
                                Catch ex As Exception
                                    status = ClassifySignatureError(ex)
                                End Try
                                If verified Then
                                    status = SignatureStatus.Valid
                                    Return
                                End If
                                tmp.Refresh()
                                If tmp.Exists Then tmp.Delete()
                            End If
                            Using pgp As New PGP(New EncryptionKeys(privateKey, passphrase))
                                Await pgp.DecryptFileAsync(input, tmp)
                            End Using
                        End Function)
                    Return status
                End Function)
        End Function

        ''' <summary>Signs a file (signature embedded in the output, not encrypted).</summary>
        Public Shared Function SignFileAsync(inputPath As String, outputPath As String, signerPrivateKey As String,
                                             passphrase As String, Optional armor As Boolean = True) As Task
            Return Task.Run(
                Function() WriteAtomicAsync(outputPath,
                    Async Function(tmp As FileInfo) As Task
                        Using pgp As New PGP(New EncryptionKeys(signerPrivateKey, passphrase))
                            Await pgp.SignFileAsync(New FileInfo(inputPath), tmp, armor)
                        End Using
                    End Function))
        End Function

        Public Shared Function VerifyFileAsync(signedFilePath As String, signerPublicKey As String) As Task(Of Boolean)
            Return Task.Run(
                Async Function() As Task(Of Boolean)
                    Using pgp As New PGP(New EncryptionKeys(signerPublicKey))
                        Try
                            Return Await pgp.VerifyFileAsync(New FileInfo(signedFilePath))
                        Catch
                            Return False
                        End Try
                    End Using
                End Function)
        End Function

        Private Shared Function ClassifySignatureError(ex As Exception) As SignatureStatus
            If ex.Message.IndexOf("not signed", StringComparison.OrdinalIgnoreCase) >= 0 Then Return SignatureStatus.NotSigned
            Return SignatureStatus.Invalid
        End Function
    End Class
End Namespace
