Imports Org.BouncyCastle.Bcpg
Imports Org.BouncyCastle.Bcpg.OpenPgp
Imports System.IO
Imports System.Text

Namespace Services
    Public Class KeyValidationResult
        Public Property IsValid As Boolean
        Public Property ErrorMessage As String = String.Empty
        Public Property KeyInfo As String = String.Empty
        Public Property Fingerprint As String = String.Empty
        Public Property UserId As String = String.Empty
        ''' <summary>UTC time at which the key (or its only encryption key) expires; Nothing = never.</summary>
        Public Property ExpiresAtUtc As DateTime?
        Public Property IsRevoked As Boolean
        ''' <summary>Public keys only: False if no usable (non-revoked) encryption key exists.</summary>
        Public Property HasEncryptionKey As Boolean = True

        Public Function GetExpiryStatus(nowUtc As DateTime) As KeyExpiryStatus
            Return KeyExpiry.Evaluate(ExpiresAtUtc, nowUtc)
        End Function
    End Class

    Public Class KeyValidator

        Public Shared Function ValidatePrivateKey(keyText As String) As KeyValidationResult
            Dim result As New KeyValidationResult()

            If keyText Is Nothing OrElse Not keyText.Contains("-----BEGIN PGP PRIVATE KEY BLOCK-----") Then
                result.IsValid = False
                result.ErrorMessage = "Missing -----BEGIN PGP PRIVATE KEY BLOCK----- header."
                Return result
            End If

            Try
                Using stream = PgpUtilities.GetDecoderStream(New MemoryStream(Encoding.UTF8.GetBytes(keyText)))
                    Dim bundle As New PgpSecretKeyRingBundle(stream)
                    Dim masterKey As PgpSecretKey = Nothing

                    For Each ring As PgpSecretKeyRing In bundle.GetKeyRings()
                        For Each key As PgpSecretKey In ring.GetSecretKeys()
                            If key.IsMasterKey Then
                                masterKey = key
                                Exit For
                            End If
                        Next
                        If masterKey IsNot Nothing Then Exit For
                    Next

                    If masterKey Is Nothing Then
                        result.IsValid = False
                        result.ErrorMessage = "No master key found in key block."
                        Return result
                    End If

                    result.IsValid = True
                    FillDetails(result, masterKey.PublicKey)
                    result.ExpiresAtUtc = ExpiryOf(masterKey.PublicKey)
                End Using
            Catch ex As Exception
                result.IsValid = False
                result.ErrorMessage = ex.Message
            End Try

            Return result
        End Function

        Public Shared Function ValidatePublicKey(keyText As String) As KeyValidationResult
            Dim result As New KeyValidationResult()

            If keyText Is Nothing OrElse Not keyText.Contains("-----BEGIN PGP PUBLIC KEY BLOCK-----") Then
                result.IsValid = False
                result.ErrorMessage = "Missing -----BEGIN PGP PUBLIC KEY BLOCK----- header."
                Return result
            End If

            Try
                Using stream = PgpUtilities.GetDecoderStream(New MemoryStream(Encoding.UTF8.GetBytes(keyText)))
                    Dim bundle As New PgpPublicKeyRingBundle(stream)
                    Dim masterKey As PgpPublicKey = Nothing
                    Dim masterRing As PgpPublicKeyRing = Nothing

                    For Each ring As PgpPublicKeyRing In bundle.GetKeyRings()
                        For Each key As PgpPublicKey In ring.GetPublicKeys()
                            If key.IsMasterKey Then
                                masterKey = key
                                masterRing = ring
                                Exit For
                            End If
                        Next
                        If masterKey IsNot Nothing Then Exit For
                    Next

                    If masterKey Is Nothing Then
                        result.IsValid = False
                        result.ErrorMessage = "No master key found in key block."
                        Return result
                    End If

                    result.IsValid = True
                    FillDetails(result, masterKey)
                    FillEncryptionExpiry(result, masterKey, masterRing)
                End Using
            Catch ex As Exception
                result.IsValid = False
                result.ErrorMessage = ex.Message
            End Try

            Return result
        End Function

        Private Shared Sub FillDetails(result As KeyValidationResult, pubKey As PgpPublicKey)
            result.KeyInfo = BuildKeyInfo(pubKey)
            result.Fingerprint = Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(pubKey.GetFingerprint()).ToUpperInvariant()
            result.IsRevoked = pubKey.IsRevoked()
            For Each uid As Object In pubKey.GetUserIds()
                result.UserId = uid.ToString()
                Exit For
            Next
        End Sub

        Private Shared Function ExpiryOf(key As PgpPublicKey) As DateTime?
            Dim seconds = key.GetValidSeconds()
            If seconds <= 0 Then Return Nothing
            Return DateTime.SpecifyKind(key.CreationTime, DateTimeKind.Utc).AddSeconds(seconds)
        End Function

        ''' <summary>
        ''' Effective expiry for encrypting: the master key must be valid AND at least one
        ''' non-revoked encryption key must be valid (latest encryption-key expiry counts).
        ''' </summary>
        Private Shared Sub FillEncryptionExpiry(result As KeyValidationResult, master As PgpPublicKey, ring As PgpPublicKeyRing)
            Dim encExpiry As DateTime? = Nothing
            Dim found As Boolean = False
            Dim neverExpires As Boolean = False

            For Each key As PgpPublicKey In ring.GetPublicKeys()
                If Not key.IsEncryptionKey OrElse key.IsRevoked() Then Continue For
                found = True
                Dim e = ExpiryOf(key)
                If Not e.HasValue Then
                    neverExpires = True
                ElseIf Not encExpiry.HasValue OrElse e.Value > encExpiry.Value Then
                    encExpiry = e
                End If
            Next

            result.HasEncryptionKey = found
            If neverExpires Then encExpiry = Nothing
            result.ExpiresAtUtc = KeyExpiry.Earliest(ExpiryOf(master), If(found, encExpiry, Nothing))
        End Sub

        Private Shared Function BuildKeyInfo(pubKey As PgpPublicKey) As String
            Dim parts As New List(Of String)()

            Dim algo = AlgorithmName(pubKey.Algorithm)
            If pubKey.BitStrength > 0 Then
                parts.Add(algo & " " & pubKey.BitStrength.ToString())
            Else
                parts.Add(algo)
            End If

            For Each uid As Object In pubKey.GetUserIds()
                parts.Add(uid.ToString())
                Exit For
            Next

            parts.Add(pubKey.CreationTime.ToString("yyyy-MM-dd"))
            Return String.Join("  ·  ", parts)
        End Function

        Private Shared Function AlgorithmName(tag As PublicKeyAlgorithmTag) As String
            Select Case tag
                Case PublicKeyAlgorithmTag.RsaGeneral, PublicKeyAlgorithmTag.RsaEncrypt, PublicKeyAlgorithmTag.RsaSign
                    Return "RSA"
                Case PublicKeyAlgorithmTag.Dsa
                    Return "DSA"
                Case PublicKeyAlgorithmTag.ElGamalEncrypt, PublicKeyAlgorithmTag.ElGamalGeneral
                    Return "ElGamal"
                Case PublicKeyAlgorithmTag.ECDH
                    Return "ECDH"
                Case PublicKeyAlgorithmTag.ECDsa
                    Return "ECDSA"
                Case PublicKeyAlgorithmTag.EdDsa
                    Return "EdDSA"
                Case Else
                    Return "Key(" & CInt(tag).ToString() & ")"
            End Select
        End Function
    End Class
End Namespace
