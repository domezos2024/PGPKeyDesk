Imports Org.BouncyCastle.Bcpg
Imports Org.BouncyCastle.Bcpg.OpenPgp
Imports System.IO
Imports System.Text

Namespace Services
    Public Class KeyValidationResult
        Public Property IsValid As Boolean
        Public Property ErrorMessage As String = String.Empty
        Public Property KeyInfo As String = String.Empty
    End Class

    Public Class KeyValidator

        Public Shared Function ValidatePrivateKey(keyText As String) As KeyValidationResult
            Dim result As New KeyValidationResult()

            If Not keyText.Contains("-----BEGIN PGP PRIVATE KEY BLOCK-----") Then
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
                    result.KeyInfo = BuildKeyInfo(masterKey.PublicKey)
                End Using
            Catch ex As Exception
                result.IsValid = False
                result.ErrorMessage = ex.Message
            End Try

            Return result
        End Function

        Public Shared Function ValidatePublicKey(keyText As String) As KeyValidationResult
            Dim result As New KeyValidationResult()

            If Not keyText.Contains("-----BEGIN PGP PUBLIC KEY BLOCK-----") Then
                result.IsValid = False
                result.ErrorMessage = "Missing -----BEGIN PGP PUBLIC KEY BLOCK----- header."
                Return result
            End If

            Try
                Using stream = PgpUtilities.GetDecoderStream(New MemoryStream(Encoding.UTF8.GetBytes(keyText)))
                    Dim bundle As New PgpPublicKeyRingBundle(stream)
                    Dim masterKey As PgpPublicKey = Nothing

                    For Each ring As PgpPublicKeyRing In bundle.GetKeyRings()
                        For Each key As PgpPublicKey In ring.GetPublicKeys()
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
                    result.KeyInfo = BuildKeyInfo(masterKey)
                End Using
            Catch ex As Exception
                result.IsValid = False
                result.ErrorMessage = ex.Message
            End Try

            Return result
        End Function

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
