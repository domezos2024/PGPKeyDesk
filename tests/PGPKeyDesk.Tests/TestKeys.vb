Imports System.IO
Imports System.Text
Imports PgpCore

''' <summary>Generates throw-away RSA test keys once per test run (generation takes a moment).</summary>
Public Class TestKeys
    Public Class Pair
        Public Property Public_ As String
        Public Property Private_ As String
        Public Property Passphrase As String
    End Class

    Private Shared ReadOnly _cache As New Dictionary(Of String, Pair)
    Private Shared ReadOnly _lock As New Object

    Public Shared Function Get_(identity As String, Optional passphrase As String = "test-pass", Optional expirySeconds As Long = 0) As Pair
        SyncLock _lock
            Dim cacheKey = identity & "|" & expirySeconds
            If Not _cache.ContainsKey(cacheKey) Then
                Using pgp As New PGP()
                    Using pub As New MemoryStream(), priv As New MemoryStream()
                        pgp.GenerateKey(pub, priv, identity, passphrase, 2048, keyExpirationInSeconds:=expirySeconds)
                        _cache(cacheKey) = New Pair With {
                            .Public_ = Encoding.UTF8.GetString(pub.ToArray()),
                            .Private_ = Encoding.UTF8.GetString(priv.ToArray()),
                            .Passphrase = passphrase}
                    End Using
                End Using
            End If
            Return _cache(cacheKey)
        End SyncLock
    End Function

    Public Shared ReadOnly Property Alice As Pair
        Get
            Return Get_("Alice <alice@example.test>", "alice-pass")
        End Get
    End Property

    Public Shared ReadOnly Property Bob As Pair
        Get
            Return Get_("Bob <bob@example.test>", "bob-pass")
        End Get
    End Property

    Public Shared ReadOnly Property Eve As Pair
        Get
            Return Get_("Eve <eve@example.test>", "eve-pass")
        End Get
    End Property

    ''' <summary>Key valid for 10 days from creation.</summary>
    Public Shared ReadOnly Property ShortLived As Pair
        Get
            Return Get_("Short <short@example.test>", "short-pass", 10L * 24 * 3600)
        End Get
    End Property
End Class
