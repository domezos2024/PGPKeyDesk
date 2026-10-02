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
            Dim r As New KeyCheckResult()
            Dim v = KeyValidator.ValidatePublicKey(publicKey)
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

        Public Shared Async Function EncryptTextAsync(plainText As String, recipientPublicKey As String,
                                                      Optional signerPrivateKey As String = Nothing,
                                                      Optional signerPassphrase As String = Nothing) As Task(Of String)
            RequireUsable(recipientPublicKey)
            If String.IsNullOrEmpty(signerPrivateKey) Then
                Using pgp As New PGP(New EncryptionKeys(recipientPublicKey))
                    Return Await pgp.EncryptArmoredStringAsync(plainText)
                End Using
            End If
            Using pgp As New PGP(New EncryptionKeys(recipientPublicKey, signerPrivateKey, signerPassphrase))
                Return Await pgp.EncryptArmoredStringAndSignAsync(plainText)
            End Using
        End Function

        ''' <summary>Decrypts; if senderPublicKey is given, the signature is verified as well.</summary>
        Public Shared Async Function DecryptTextAsync(cipherText As String, privateKey As String, passphrase As String,
                                                      Optional senderPublicKey As String = Nothing) As Task(Of DecryptResult)
            Dim result As New DecryptResult()
            Using pgp As New PGP(New EncryptionKeys(privateKey, passphrase))
                result.Text = Await pgp.DecryptArmoredStringAsync(cipherText)
            End Using

            If String.IsNullOrWhiteSpace(senderPublicKey) Then Return result

            Try
                Using pgp As New PGP(New EncryptionKeys(senderPublicKey, privateKey, passphrase))
                    Await pgp.DecryptArmoredStringAndVerifyAsync(cipherText)
                End Using
                result.Signature = SignatureStatus.Valid
            Catch ex As Exception
                result.Signature = ClassifySignatureError(ex)
            End Try
            Return result
        End Function

        Public Shared Async Function SignTextAsync(text As String, signerPrivateKey As String, passphrase As String) As Task(Of String)
            Using pgp As New PGP(New EncryptionKeys(signerPrivateKey, passphrase))
                Return Await pgp.ClearSignArmoredStringAsync(text)
            End Using
        End Function

        Public Shared Async Function VerifyTextAsync(clearSignedText As String, signerPublicKey As String) As Task(Of VerifyResult)
            Using pgp As New PGP(New EncryptionKeys(signerPublicKey))
                Dim ok As Boolean
                Try
                    ok = Await pgp.VerifyClearArmoredStringAsync(clearSignedText)
                Catch ex As Exception
                    ok = False
                End Try
                Return New VerifyResult With {.IsValid = ok}
            End Using
        End Function

        ' ---------------------------------------------------------------- files

        Public Shared Async Function EncryptFileAsync(inputPath As String, outputPath As String, recipientPublicKey As String,
                                                      Optional signerPrivateKey As String = Nothing,
                                                      Optional signerPassphrase As String = Nothing,
                                                      Optional armor As Boolean = False) As Task
            RequireUsable(recipientPublicKey)
            Dim input As New FileInfo(inputPath)
            Dim output As New FileInfo(outputPath)
            If String.IsNullOrEmpty(signerPrivateKey) Then
                Using pgp As New PGP(New EncryptionKeys(recipientPublicKey))
                    Await pgp.EncryptFileAsync(input, output, armor)
                End Using
            Else
                Using pgp As New PGP(New EncryptionKeys(recipientPublicKey, signerPrivateKey, signerPassphrase))
                    Await pgp.EncryptFileAndSignAsync(input, output, armor)
                End Using
            End If
        End Function

        ''' <summary>Decrypts a file; with senderPublicKey the signature is verified (status returned).</summary>
        Public Shared Async Function DecryptFileAsync(inputPath As String, outputPath As String, privateKey As String, passphrase As String,
                                                      Optional senderPublicKey As String = Nothing) As Task(Of SignatureStatus)
            Dim input As New FileInfo(inputPath)
            Dim output As New FileInfo(outputPath)
            Using pgp As New PGP(New EncryptionKeys(privateKey, passphrase))
                Await pgp.DecryptFileAsync(input, output)
            End Using

            If String.IsNullOrWhiteSpace(senderPublicKey) Then Return SignatureStatus.NotChecked

            Dim scratch = Path.GetTempFileName()
            Try
                Using pgp As New PGP(New EncryptionKeys(senderPublicKey, privateKey, passphrase))
                    Await pgp.DecryptFileAndVerifyAsync(input, New FileInfo(scratch))
                End Using
                Return SignatureStatus.Valid
            Catch ex As Exception
                Return ClassifySignatureError(ex)
            Finally
                Try
                    File.Delete(scratch)
                Catch
                End Try
            End Try
        End Function

        ''' <summary>Signs a file (signature embedded in the output, not encrypted).</summary>
        Public Shared Async Function SignFileAsync(inputPath As String, outputPath As String, signerPrivateKey As String,
                                                   passphrase As String, Optional armor As Boolean = True) As Task
            Using pgp As New PGP(New EncryptionKeys(signerPrivateKey, passphrase))
                Await pgp.SignFileAsync(New FileInfo(inputPath), New FileInfo(outputPath), armor)
            End Using
        End Function

        Public Shared Async Function VerifyFileAsync(signedFilePath As String, signerPublicKey As String) As Task(Of Boolean)
            Using pgp As New PGP(New EncryptionKeys(signerPublicKey))
                Try
                    Return Await pgp.VerifyFileAsync(New FileInfo(signedFilePath))
                Catch
                    Return False
                End Try
            End Using
        End Function

        Private Shared Function ClassifySignatureError(ex As Exception) As SignatureStatus
            If ex.Message.IndexOf("not signed", StringComparison.OrdinalIgnoreCase) >= 0 Then Return SignatureStatus.NotSigned
            Return SignatureStatus.Invalid
        End Function
    End Class
End Namespace
