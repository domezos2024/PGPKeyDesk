Imports System

Namespace Models
    Public Class PGPProfile
        Public Property Id As String = Guid.NewGuid().ToString()
        Public Property Name As String = String.Empty
        Public Property PrivateKey As String = String.Empty
        Public Property PublicKey As String = String.Empty
        Public Property CreatedAt As DateTime = DateTime.Now

        Public Overrides Function ToString() As String
            Return Name
        End Function
    End Class
End Namespace
