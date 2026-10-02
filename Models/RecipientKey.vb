Imports System

Namespace Models
    ''' <summary>A recipient's public key in the keyring.</summary>
    Public Class RecipientKey
        Public Property Id As String = Guid.NewGuid().ToString()
        Public Property Name As String = String.Empty
        Public Property Fingerprint As String = String.Empty
        Public Property PublicKey As String = String.Empty
        Public Property AddedAt As DateTime = DateTime.UtcNow

        Public Overrides Function ToString() As String
            Return Name
        End Function
    End Class
End Namespace
